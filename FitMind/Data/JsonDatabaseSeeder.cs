using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FitMind.Data;

/// <summary>Заполняет новую SQL Server базу таблицами из JSONData/seed-manifest.json.</summary>
public static class JsonDatabaseSeeder
{
    public static async Task ImportAsync(ApplicationDbContext db, string webRootPath)
    {
        var jsonDataDirectory = Path.Combine(AppContext.BaseDirectory, "JSONData");
        var tables = ConvertLegacyCategories(await ReadSeedTablesAsync(jsonDataDirectory));
        ValidateImageReferences(tables, webRootPath);

        var connection = db.Database.GetDbConnection();
        var closeConnectionAfterImport = connection.State != ConnectionState.Open;
        if (closeConnectionAfterImport)
            await db.Database.OpenConnectionAsync();

        try
        {
            var containsCurrentSeedVersion = tables
                .Where(table => table.Name == "Настройки_системы")
                .SelectMany(table => table.Rows.EnumerateArray())
                .Any(row => row.TryGetProperty("Ключ", out var key)
                    && key.GetString() == "SeedVersion"
                    && row.TryGetProperty("Значение", out var value)
                    && value.GetString() == "3");
            if (!containsCurrentSeedVersion)
                throw new InvalidDataException("В JSON-начальных данных отсутствует актуальная отметка SeedVersion=3.");

            await using var transaction = await db.Database.BeginTransactionAsync();
            var dbTransaction = transaction.GetDbTransaction();
            var existingTables = new List<SeedTable>();
            foreach (var table in tables)
                if ((await ReadTableColumnsAsync(connection, dbTransaction, table.Name)).Count > 0)
                    existingTables.Add(table);
            foreach (var table in existingTables)
                await SetConstraintsAsync(connection, dbTransaction, table.Name, enabled: false);

            var tableCount = 0;
            var rowCount = 0;
            foreach (var table in existingTables)
            {
                var tableName = table.Name;
                var allowedColumns = await ReadTableColumnsAsync(connection, dbTransaction, tableName);
                var quotedTable = QuoteIdentifier(tableName);
                var hasIdentity = await HasIdentityColumnAsync(connection, dbTransaction, tableName);
                if (hasIdentity)
                    await ExecuteAsync(connection, dbTransaction, $"SET IDENTITY_INSERT {quotedTable} ON;");
                try
                {
                    foreach (var row in table.Rows.EnumerateArray())
                    {
                        if (row.ValueKind != JsonValueKind.Object)
                            throw new InvalidDataException($"Строка таблицы «{tableName}» должна быть JSON-объектом.");

                        var fields = row.EnumerateObject().ToArray();
                        if (fields.Length == 0)
                            continue;
                        foreach (var field in fields)
                        {
                            if (!allowedColumns.Contains(field.Name))
                                throw new InvalidDataException(
                                    $"В таблице «{tableName}» из JSON неизвестный столбец «{field.Name}».");
                        }

                        var columnSql = string.Join(", ", fields.Select(field => QuoteIdentifier(field.Name)));
                        var parameterSql = string.Join(", ", fields.Select((_, index) => $"@p{index}"));
                        await using var command = connection.CreateCommand();
                        command.Transaction = dbTransaction;
                        command.CommandText = $"INSERT INTO {quotedTable} ({columnSql}) VALUES ({parameterSql});";
                        for (var index = 0; index < fields.Length; index++)
                        {
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = $"@p{index}";
                            parameter.Value = ToDatabaseValue(fields[index].Value);
                            command.Parameters.Add(parameter);
                        }
                        await command.ExecuteNonQueryAsync();
                        rowCount++;
                    }
                }
                finally
                {
                    if (hasIdentity)
                        await ExecuteAsync(connection, dbTransaction, $"SET IDENTITY_INSERT {quotedTable} OFF;");
                }
                tableCount++;
            }

            foreach (var table in existingTables.AsEnumerable().Reverse())
                await SetConstraintsAsync(connection, dbTransaction, table.Name, enabled: true);

            await transaction.CommitAsync();
            Console.WriteLine($"FitMind SQL Server заполнена из JSON: {tableCount} таблиц, {rowCount} строк.");
        }
        finally
        {
            if (closeConnectionAfterImport)
                await db.Database.CloseConnectionAsync();
        }
    }

    private static IReadOnlyList<SeedTable> ConvertLegacyCategories(IReadOnlyList<SeedTable> tables)
    {
        var categorySources = new Dictionary<string, (string Section, int Offset)>(StringComparer.Ordinal)
        {
            ["Категории_упражнений"] = ("Упражнения", 0),
            ["Категории_продуктов"] = ("Продукты", 100_000),
            ["Категории_добавок"] = ("Добавки", 200_000),
            ["Категории_статей"] = ("Статьи", 300_000)
        };
        var dependentTables = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Упражнения"] = 0,
            ["Продукты"] = 100_000,
            ["Спортивные_добавки"] = 200_000,
            ["Статьи"] = 300_000
        };

        var categories = new List<Dictionary<string, object?>>();
        foreach (var table in tables.Where(table => categorySources.ContainsKey(table.Name)))
        {
            var (section, offset) = categorySources[table.Name];
            foreach (var row in table.Rows.EnumerateArray())
            {
                categories.Add(new Dictionary<string, object?>
                {
                    ["ID_категории"] = row.GetProperty("ID_категории").GetInt32() + offset,
                    ["Раздел"] = section,
                    ["Название"] = row.GetProperty("Название").GetString(),
                    ["Описание"] = row.TryGetProperty("Описание", out var description)
                        && description.ValueKind != JsonValueKind.Null
                            ? description.GetString()
                            : null
                });
            }
        }

        var converted = new List<SeedTable>();
        if (categories.Count > 0)
            converted.Add(new SeedTable("Категории", JsonSerializer.SerializeToElement(categories)));

        foreach (var table in tables)
        {
            if (categorySources.ContainsKey(table.Name))
                continue;
            if (!dependentTables.TryGetValue(table.Name, out var offset) || offset == 0)
            {
                converted.Add(table);
                continue;
            }

            var rows = table.Rows.EnumerateArray().Select(row =>
            {
                var fields = row.EnumerateObject().ToDictionary(
                    field => field.Name,
                    field => field.Value.Clone(),
                    StringComparer.Ordinal);
                if (fields.TryGetValue("ID_категории", out var categoryId)
                    && categoryId.ValueKind == JsonValueKind.Number)
                    fields["ID_категории"] = JsonSerializer.SerializeToElement(categoryId.GetInt32() + offset);
                return fields;
            }).ToArray();
            converted.Add(new SeedTable(table.Name, JsonSerializer.SerializeToElement(rows)));
        }

        return converted;
    }

    private static async Task<IReadOnlyList<SeedTable>> ReadSeedTablesAsync(string jsonDataDirectory)
    {
        var manifestPath = Path.Combine(jsonDataDirectory, "seed-manifest.json");
        if (File.Exists(manifestPath))
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = await JsonDocument.ParseAsync(stream);
            var root = document.RootElement;
            if (!root.TryGetProperty("formatVersion", out var version) || version.GetInt32() != 1)
                throw new InvalidDataException("Версия формата JSONData/seed-manifest.json не поддерживается.");
            if (!root.TryGetProperty("tables", out var tableEntries) || tableEntries.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("В JSONData/seed-manifest.json отсутствует массив tables.");

            var rootPath = Path.GetFullPath(jsonDataDirectory) + Path.DirectorySeparatorChar;
            var tables = new List<SeedTable>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in tableEntries.EnumerateArray())
            {
                var name = entry.GetProperty("name").GetString();
                var relativeFile = entry.GetProperty("file").GetString();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relativeFile))
                    throw new InvalidDataException("В manifest обнаружена таблица без name или file.");
                if (!names.Add(name))
                    throw new InvalidDataException($"Таблица «{name}» указана в manifest дважды.");

                var tablePath = Path.GetFullPath(Path.Combine(jsonDataDirectory, relativeFile));
                if (!tablePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Путь «{relativeFile}» выходит за пределы JSONData.");
                if (!File.Exists(tablePath))
                    throw new FileNotFoundException($"Не найден JSON-файл таблицы «{name}».", tablePath);

                using var tableStream = File.OpenRead(tablePath);
                using var tableDocument = await JsonDocument.ParseAsync(tableStream);
                var tableRoot = tableDocument.RootElement;
                var embeddedName = tableRoot.GetProperty("table").GetString();
                if (!string.Equals(name, embeddedName, StringComparison.Ordinal))
                    throw new InvalidDataException($"Имя таблицы в «{relativeFile}» не совпадает с manifest.");
                if (!tableRoot.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException($"В «{relativeFile}» отсутствует массив rows.");

                tables.Add(new SeedTable(name, rows.Clone()));
            }
            return tables;
        }

        // Compatibility only for installations that have not yet received the split files.
        var legacyPath = Path.Combine(jsonDataDirectory, "database-seed.json");
        if (!File.Exists(legacyPath))
            throw new FileNotFoundException(
                "Не найден JSONData/seed-manifest.json и резервный database-seed.json. Проверьте JSONData.",
                manifestPath);

        using var legacyStream = File.OpenRead(legacyPath);
        using var legacyDocument = await JsonDocument.ParseAsync(legacyStream);
        var legacyRoot = legacyDocument.RootElement;
        if (!legacyRoot.TryGetProperty("formatVersion", out var legacyVersion) || legacyVersion.GetInt32() != 1)
            throw new InvalidDataException("Версия формата database-seed.json не поддерживается.");
        return legacyRoot.GetProperty("tables").EnumerateArray()
            .Select(table => new SeedTable(
                table.GetProperty("name").GetString()
                    ?? throw new InvalidDataException("В database-seed.json обнаружена таблица без имени."),
                table.GetProperty("rows").Clone()))
            .ToArray();
    }

    /// <summary>
    /// Каталожные изображения хранятся как локальные статические файлы, а в JSON
    /// сохраняется их публичный путь. Так база не дублирует бинарные данные,
    /// но каждая ссылка проверяется до записи начальных данных.
    /// </summary>
    private static void ValidateImageReferences(IEnumerable<SeedTable> tables, string webRootPath)
    {
        var webRoot = Path.GetFullPath(webRootPath);
        var webRootPrefix = webRoot + Path.DirectorySeparatorChar;

        foreach (var table in tables)
        {
            foreach (var row in table.Rows.EnumerateArray())
            {
                if (!row.TryGetProperty("Изображение", out var image)
                    || image.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    continue;

                if (image.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(image.GetString()))
                    throw new InvalidDataException($"Таблица «{table.Name}» содержит пустое или некорректное значение «Изображение».");

                var publicPath = image.GetString()!;
                if (!publicPath.StartsWith("/images/", StringComparison.Ordinal)
                    || publicPath.Contains('?')
                    || publicPath.Contains('#'))
                    throw new InvalidDataException(
                        $"Таблица «{table.Name}» содержит недопустимый путь изображения «{publicPath}». Допускаются только локальные /images/... пути.");

                var physicalPath = Path.GetFullPath(Path.Combine(webRoot, publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                if (!physicalPath.StartsWith(webRootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(physicalPath))
                    throw new FileNotFoundException(
                        $"Для таблицы «{table.Name}» не найден локальный файл изображения «{publicPath}».",
                        physicalPath);
            }
        }
    }

    private static async Task<HashSet<string>> ReadTableColumnsAsync(
        System.Data.Common.DbConnection connection,
        DbTransaction transaction,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        return columns;
    }

    private static async Task<bool> HasIdentityColumnAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sys.identity_columns WHERE object_id = OBJECT_ID(@tableName);";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = $"dbo.{tableName}";
        command.Parameters.Add(parameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static Task SetConstraintsAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName,
        bool enabled) => ExecuteAsync(connection, transaction,
            enabled
                ? $"ALTER TABLE {QuoteIdentifier(tableName)} WITH CHECK CHECK CONSTRAINT ALL;"
                : $"ALTER TABLE {QuoteIdentifier(tableName)} NOCHECK CONSTRAINT ALL;");

    private static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string name) => "[" + name.Replace("]", "]]" ) + "]";

    private static object ToDatabaseValue(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                return DBNull.Value;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.String:
                return value.GetString() ?? string.Empty;
            case JsonValueKind.Number:
                if (value.TryGetInt64(out var integerValue))
                    return integerValue;
                if (value.TryGetDecimal(out var decimalValue))
                    return decimalValue;
                return value.GetDouble();
            case JsonValueKind.Array:
            case JsonValueKind.Object:
                return value.GetRawText();
            default:
                return DBNull.Value;
        }
    }

    private sealed record SeedTable(string Name, JsonElement Rows);
}
