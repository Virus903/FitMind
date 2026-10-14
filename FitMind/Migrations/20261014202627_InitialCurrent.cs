using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitMind.Migrations
{
    /// <inheritdoc />
    public partial class InitialCurrent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Группы_мышц",
                columns: table => new
                {
                    ID_группы = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Группы_мышц", x => x.ID_группы);
                });

            migrationBuilder.CreateTable(
                name: "Категории",
                columns: table => new
                {
                    ID_категории = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Раздел = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Название = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Категории", x => x.ID_категории);
                });

            migrationBuilder.CreateTable(
                name: "Настройки_системы",
                columns: table => new
                {
                    Ключ = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Значение = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Дата_изменения = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Настройки_системы", x => x.Ключ);
                });

            migrationBuilder.CreateTable(
                name: "Пользователи",
                columns: table => new
                {
                    ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Имя_пользователя = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Нормализованное_имя_пользователя = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Электронная_почта = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Нормализованная_электронная_почта = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Почта_подтверждена = table.Column<bool>(type: "bit", nullable: false),
                    Хэш_пароля = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Метка_безопасности = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Метка_конкурентности = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Номер_телефона = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Телефон_подтверждён = table.Column<bool>(type: "bit", nullable: false),
                    Двухфакторная_аутентификация = table.Column<bool>(type: "bit", nullable: false),
                    Блокировка_до = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Блокировка_разрешена = table.Column<bool>(type: "bit", nullable: false),
                    Неудачных_попыток_входа = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Пользователи", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Роли",
                columns: table => new
                {
                    ID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Название = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Нормализованное_название = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Метка_конкурентности = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Роли", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Типы_замеров",
                columns: table => new
                {
                    ID_типа = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Единица_измерения = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Основной = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Типы_замеров", x => x.ID_типа);
                });

            migrationBuilder.CreateTable(
                name: "Уровни_активности",
                columns: table => new
                {
                    ID_активности = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Коэффициент = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Уровни_активности", x => x.ID_активности);
                });

            migrationBuilder.CreateTable(
                name: "Уровни_подготовки",
                columns: table => new
                {
                    ID_уровня = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Уровни_подготовки", x => x.ID_уровня);
                });

            migrationBuilder.CreateTable(
                name: "Форматы_тренировок",
                columns: table => new
                {
                    ID_формата = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Иконка = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Форматы_тренировок", x => x.ID_формата);
                });

            migrationBuilder.CreateTable(
                name: "Цели",
                columns: table => new
                {
                    ID_цели = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Цели", x => x.ID_цели);
                });

            migrationBuilder.CreateTable(
                name: "Статьи",
                columns: table => new
                {
                    ID_статьи = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_категории = table.Column<int>(type: "int", nullable: false),
                    Заголовок = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Краткое_описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Текст = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Советы = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Вывод = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Минут_чтения = table.Column<short>(type: "smallint", nullable: true),
                    Изображение = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Дата_публикации = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Опубликована = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Статьи", x => x.ID_статьи);
                    table.ForeignKey(
                        name: "FK_Статьи_Категории_ID_категории",
                        column: x => x.ID_категории,
                        principalTable: "Категории",
                        principalColumn: "ID_категории",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Внешние_входы",
                columns: table => new
                {
                    Поставщик_входа = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Ключ_поставщика = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Отображаемое_имя_поставщика = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ID_пользователя = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Внешние_входы", x => new { x.Поставщик_входа, x.Ключ_поставщика });
                    table.ForeignKey(
                        name: "FK_Внешние_входы_Пользователи_ID_пользователя",
                        column: x => x.ID_пользователя,
                        principalTable: "Пользователи",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Журнал_действий_администратора",
                columns: table => new
                {
                    ID_события = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_администратора = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Дата_и_время = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Действие = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Тип_сущности = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ID_сущности = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IP_адрес = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Журнал_действий_администратора", x => x.ID_события);
                    table.ForeignKey(
                        name: "FK_Журнал_действий_администратора_Пользователи_ID_администратора",
                        column: x => x.ID_администратора,
                        principalTable: "Пользователи",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Личные_заметки",
                columns: table => new
                {
                    ID_заметки = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_пользователя = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Раздел = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Заголовок = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Текст = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Дата_создания = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Личные_заметки", x => x.ID_заметки);
                    table.ForeignKey(
                        name: "FK_Личные_заметки_Пользователи_ID_пользователя",
                        column: x => x.ID_пользователя,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Методы_восстановления",
                columns: table => new
                {
                    ID_метода = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_владельца = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ID_источника = table.Column<int>(type: "int", nullable: true),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Категория = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Краткое_описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Полное_описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Польза = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Рекомендации = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Предупреждения = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Мин_длительность = table.Column<short>(type: "smallint", nullable: true),
                    Макс_длительность = table.Column<short>(type: "smallint", nullable: true),
                    Изображение = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Активно = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Методы_восстановления", x => x.ID_метода);
                    table.ForeignKey(
                        name: "FK_Методы_восстановления_Методы_восстановления_ID_источника",
                        column: x => x.ID_источника,
                        principalTable: "Методы_восстановления",
                        principalColumn: "ID_метода");
                    table.ForeignKey(
                        name: "FK_Методы_восстановления_Пользователи_ID_владельца",
                        column: x => x.ID_владельца,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Продукты",
                columns: table => new
                {
                    ID_продукта = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_категории = table.Column<int>(type: "int", nullable: false),
                    Вид = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ID_владельца = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ID_источника = table.Column<int>(type: "int", nullable: true),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Изображение = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Порция_г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Ккал_на_100г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Белки_на_100г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Жиры_на_100г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Углеводы_на_100г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Клетчатка_на_100г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Гликемический_индекс = table.Column<short>(type: "smallint", nullable: true),
                    Польза = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Советы_по_приготовлению = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Приготовление = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Время_приготовления_мин = table.Column<short>(type: "smallint", nullable: true),
                    Активно = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Продукты", x => x.ID_продукта);
                    table.ForeignKey(
                        name: "FK_Продукты_Категории_ID_категории",
                        column: x => x.ID_категории,
                        principalTable: "Категории",
                        principalColumn: "ID_категории");
                    table.ForeignKey(
                        name: "FK_Продукты_Пользователи_ID_владельца",
                        column: x => x.ID_владельца,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Продукты_Продукты_ID_источника",
                        column: x => x.ID_источника,
                        principalTable: "Продукты",
                        principalColumn: "ID_продукта");
                });

            migrationBuilder.CreateTable(
                name: "Спортивные_добавки",
                columns: table => new
                {
                    ID_добавки = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_владельца = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ID_источника = table.Column<int>(type: "int", nullable: true),
                    ID_категории = table.Column<int>(type: "int", nullable: false),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Краткое_описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Назначение = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Как_принимают = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Стандартная_дозировка = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Форма = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Оптимальное_время = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Плюсы = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Минусы = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Предупреждение = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FAQ = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Изображение = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Активно = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Спортивные_добавки", x => x.ID_добавки);
                    table.ForeignKey(
                        name: "FK_Спортивные_добавки_Категории_ID_категории",
                        column: x => x.ID_категории,
                        principalTable: "Категории",
                        principalColumn: "ID_категории",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Спортивные_добавки_Пользователи_ID_владельца",
                        column: x => x.ID_владельца,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Спортивные_добавки_Спортивные_добавки_ID_источника",
                        column: x => x.ID_источника,
                        principalTable: "Спортивные_добавки",
                        principalColumn: "ID_добавки");
                });

            migrationBuilder.CreateTable(
                name: "Токены_пользователей",
                columns: table => new
                {
                    ID_пользователя = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Поставщик_входа = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Название = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Значение = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Токены_пользователей", x => new { x.ID_пользователя, x.Поставщик_входа, x.Название });
                    table.ForeignKey(
                        name: "FK_Токены_пользователей_Пользователи_ID_пользователя",
                        column: x => x.ID_пользователя,
                        principalTable: "Пользователи",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Уведомления",
                columns: table => new
                {
                    ID_уведомления = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_пользователя = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Тип = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Заголовок = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Текст = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ссылка = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Прочитано = table.Column<bool>(type: "bit", nullable: false),
                    Дата_создания = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Уведомления", x => x.ID_уведомления);
                    table.ForeignKey(
                        name: "FK_Уведомления_Пользователи_ID_пользователя",
                        column: x => x.ID_пользователя,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Упражнения",
                columns: table => new
                {
                    ID_упражнения = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_категории = table.Column<int>(type: "int", nullable: false),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Техника = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Частые_ошибки = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Рекомендации = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Противопоказания = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Необходимое_оборудование = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Место = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Уровень_сложности = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Расход_ккал_час = table.Column<int>(type: "int", nullable: true),
                    Изображение = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Видео = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Активно = table.Column<bool>(type: "bit", nullable: false),
                    ID_владельца = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ID_исходного_упражнения = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Упражнения", x => x.ID_упражнения);
                    table.ForeignKey(
                        name: "FK_Упражнения_Категории_ID_категории",
                        column: x => x.ID_категории,
                        principalTable: "Категории",
                        principalColumn: "ID_категории",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Упражнения_Пользователи_ID_владельца",
                        column: x => x.ID_владельца,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Упражнения_Упражнения_ID_исходного_упражнения",
                        column: x => x.ID_исходного_упражнения,
                        principalTable: "Упражнения",
                        principalColumn: "ID_упражнения");
                });

            migrationBuilder.CreateTable(
                name: "Утверждения_пользователей",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_пользователя = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Тип_утверждения = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Значение_утверждения = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Утверждения_пользователей", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Утверждения_пользователей_Пользователи_ID_пользователя",
                        column: x => x.ID_пользователя,
                        principalTable: "Пользователи",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Пользователи_Роли",
                columns: table => new
                {
                    ID_пользователя = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ID_роли = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Пользователи_Роли", x => new { x.ID_пользователя, x.ID_роли });
                    table.ForeignKey(
                        name: "FK_Пользователи_Роли_Пользователи_ID_пользователя",
                        column: x => x.ID_пользователя,
                        principalTable: "Пользователи",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Пользователи_Роли_Роли_ID_роли",
                        column: x => x.ID_роли,
                        principalTable: "Роли",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Утверждения_ролей",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_роли = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Тип_утверждения = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Значение_утверждения = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Утверждения_ролей", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Утверждения_ролей_Роли_ID_роли",
                        column: x => x.ID_роли,
                        principalTable: "Роли",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Замеры_тела",
                columns: table => new
                {
                    ID_замера = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_пользователя = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ID_типа = table.Column<int>(type: "int", nullable: false),
                    Дата_замера = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Значение = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Комментарий = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Замеры_тела", x => x.ID_замера);
                    table.ForeignKey(
                        name: "FK_Замеры_тела_Пользователи_ID_пользователя",
                        column: x => x.ID_пользователя,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Замеры_тела_Типы_замеров_ID_типа",
                        column: x => x.ID_типа,
                        principalTable: "Типы_замеров",
                        principalColumn: "ID_типа",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Программы",
                columns: table => new
                {
                    ID_программы = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Вид = table.Column<int>(type: "int", nullable: false),
                    ID_владельца = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ID_исходной_программы = table.Column<int>(type: "int", nullable: true),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Для_кого = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Цель = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ID_уровня = table.Column<int>(type: "int", nullable: true),
                    Место = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Недель = table.Column<byte>(type: "tinyint", nullable: true),
                    Тренировок_в_неделю = table.Column<byte>(type: "tinyint", nullable: true),
                    Средняя_длительность_мин = table.Column<short>(type: "smallint", nullable: true),
                    Ккал_в_день = table.Column<int>(type: "int", nullable: true),
                    Вода_мл = table.Column<int>(type: "int", nullable: true),
                    Изображение = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Активна = table.Column<bool>(type: "bit", nullable: false),
                    Дата_начала = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Дата_окончания = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Создано = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Программы", x => x.ID_программы);
                    table.ForeignKey(
                        name: "FK_Программы_Пользователи_ID_владельца",
                        column: x => x.ID_владельца,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Программы_Программы_ID_исходной_программы",
                        column: x => x.ID_исходной_программы,
                        principalTable: "Программы",
                        principalColumn: "ID_программы");
                    table.ForeignKey(
                        name: "FK_Программы_Уровни_подготовки_ID_уровня",
                        column: x => x.ID_уровня,
                        principalTable: "Уровни_подготовки",
                        principalColumn: "ID_уровня");
                });

            migrationBuilder.CreateTable(
                name: "Пользовательские_форматы_тренировок",
                columns: table => new
                {
                    ID_пользователя = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ID_формата = table.Column<int>(type: "int", nullable: false),
                    Дата_добавления = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Пользовательские_форматы_тренировок", x => new { x.ID_пользователя, x.ID_формата });
                    table.ForeignKey(
                        name: "FK_Пользовательские_форматы_тренировок_Пользователи_ID_пользователя",
                        column: x => x.ID_пользователя,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Пользовательские_форматы_тренировок_Форматы_тренировок_ID_формата",
                        column: x => x.ID_формата,
                        principalTable: "Форматы_тренировок",
                        principalColumn: "ID_формата",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Профили",
                columns: table => new
                {
                    ID_профиля = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_пользователя = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Имя = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Дата_рождения = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Возраст = table.Column<byte>(type: "tinyint", nullable: true),
                    Пол = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Рост_см = table.Column<short>(type: "smallint", nullable: true),
                    Вес_кг = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    О_себе = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ID_уровня_подготовки = table.Column<int>(type: "int", nullable: true),
                    Дома = table.Column<bool>(type: "bit", nullable: false),
                    На_улице = table.Column<bool>(type: "bit", nullable: false),
                    В_зале = table.Column<bool>(type: "bit", nullable: false),
                    ID_цели = table.Column<int>(type: "int", nullable: true),
                    ID_активности = table.Column<int>(type: "int", nullable: true),
                    Тренировок_в_неделю = table.Column<byte>(type: "tinyint", nullable: true),
                    Цель_сна_часов = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Шаг_анкеты = table.Column<byte>(type: "tinyint", nullable: false),
                    Анкета_завершена = table.Column<bool>(type: "bit", nullable: false),
                    Дата_создания = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Тема = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Единицы = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Уведомления = table.Column<bool>(type: "bit", nullable: false),
                    Email_уведомления = table.Column<bool>(type: "bit", nullable: false),
                    Тихие_часы = table.Column<bool>(type: "bit", nullable: false),
                    Тихие_часы_начало = table.Column<TimeSpan>(type: "time", nullable: true),
                    Тихие_часы_конец = table.Column<TimeSpan>(type: "time", nullable: true),
                    Цель_воды_мл = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Профили", x => x.ID_профиля);
                    table.ForeignKey(
                        name: "FK_Профили_Пользователи_ID_пользователя",
                        column: x => x.ID_пользователя,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Профили_Уровни_активности_ID_активности",
                        column: x => x.ID_активности,
                        principalTable: "Уровни_активности",
                        principalColumn: "ID_активности");
                    table.ForeignKey(
                        name: "FK_Профили_Уровни_подготовки_ID_уровня_подготовки",
                        column: x => x.ID_уровня_подготовки,
                        principalTable: "Уровни_подготовки",
                        principalColumn: "ID_уровня");
                    table.ForeignKey(
                        name: "FK_Профили_Цели_ID_цели",
                        column: x => x.ID_цели,
                        principalTable: "Цели",
                        principalColumn: "ID_цели");
                });

            migrationBuilder.CreateTable(
                name: "Состав_рецепта",
                columns: table => new
                {
                    ID_строки = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_рецепта = table.Column<int>(type: "int", nullable: false),
                    ID_продукта = table.Column<int>(type: "int", nullable: true),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Граммы = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Порядок = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Состав_рецепта", x => x.ID_строки);
                    table.ForeignKey(
                        name: "FK_Состав_рецепта_Продукты_ID_продукта",
                        column: x => x.ID_продукта,
                        principalTable: "Продукты",
                        principalColumn: "ID_продукта");
                    table.ForeignKey(
                        name: "FK_Состав_рецепта_Продукты_ID_рецепта",
                        column: x => x.ID_рецепта,
                        principalTable: "Продукты",
                        principalColumn: "ID_продукта",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Упражнения_Мышцы",
                columns: table => new
                {
                    ID_упражнения = table.Column<int>(type: "int", nullable: false),
                    ID_группы = table.Column<int>(type: "int", nullable: false),
                    Основная_группа = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Упражнения_Мышцы", x => new { x.ID_упражнения, x.ID_группы });
                    table.ForeignKey(
                        name: "FK_Упражнения_Мышцы_Группы_мышц_ID_группы",
                        column: x => x.ID_группы,
                        principalTable: "Группы_мышц",
                        principalColumn: "ID_группы",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Упражнения_Мышцы_Упражнения_ID_упражнения",
                        column: x => x.ID_упражнения,
                        principalTable: "Упражнения",
                        principalColumn: "ID_упражнения",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Пункты_программы",
                columns: table => new
                {
                    ID_пункта = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_программы = table.Column<int>(type: "int", nullable: false),
                    Номер_дня = table.Column<byte>(type: "tinyint", nullable: false),
                    День_недели = table.Column<int>(type: "int", nullable: true),
                    Время = table.Column<TimeSpan>(type: "time", nullable: true),
                    Тип = table.Column<int>(type: "int", nullable: false),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Описание = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ID_продукта = table.Column<int>(type: "int", nullable: true),
                    ID_добавки = table.Column<int>(type: "int", nullable: true),
                    ID_метода_восстановления = table.Column<int>(type: "int", nullable: true),
                    Доза = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ккал = table.Column<int>(type: "int", nullable: true),
                    Вода_мл = table.Column<int>(type: "int", nullable: true),
                    Минут = table.Column<short>(type: "smallint", nullable: true),
                    Порядок = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Пункты_программы", x => x.ID_пункта);
                    table.ForeignKey(
                        name: "FK_Пункты_программы_Методы_восстановления_ID_метода_восстановления",
                        column: x => x.ID_метода_восстановления,
                        principalTable: "Методы_восстановления",
                        principalColumn: "ID_метода");
                    table.ForeignKey(
                        name: "FK_Пункты_программы_Программы_ID_программы",
                        column: x => x.ID_программы,
                        principalTable: "Программы",
                        principalColumn: "ID_программы",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Пункты_программы_Продукты_ID_продукта",
                        column: x => x.ID_продукта,
                        principalTable: "Продукты",
                        principalColumn: "ID_продукта");
                    table.ForeignKey(
                        name: "FK_Пункты_программы_Спортивные_добавки_ID_добавки",
                        column: x => x.ID_добавки,
                        principalTable: "Спортивные_добавки",
                        principalColumn: "ID_добавки");
                });

            migrationBuilder.CreateTable(
                name: "Записи_дня",
                columns: table => new
                {
                    ID_записи = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_пользователя = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Начало = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Окончание = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Тип = table.Column<int>(type: "int", nullable: false),
                    ID_пункта_программы = table.Column<int>(type: "int", nullable: true),
                    ID_метода_восстановления = table.Column<int>(type: "int", nullable: true),
                    ID_добавки = table.Column<int>(type: "int", nullable: true),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Статус = table.Column<int>(type: "int", nullable: false),
                    Значение = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Оценка = table.Column<byte>(type: "tinyint", nullable: true),
                    Комментарий = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Записи_дня", x => x.ID_записи);
                    table.ForeignKey(
                        name: "FK_Записи_дня_Методы_восстановления_ID_метода_восстановления",
                        column: x => x.ID_метода_восстановления,
                        principalTable: "Методы_восстановления",
                        principalColumn: "ID_метода");
                    table.ForeignKey(
                        name: "FK_Записи_дня_Пользователи_ID_пользователя",
                        column: x => x.ID_пользователя,
                        principalTable: "Пользователи",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Записи_дня_Пункты_программы_ID_пункта_программы",
                        column: x => x.ID_пункта_программы,
                        principalTable: "Пункты_программы",
                        principalColumn: "ID_пункта",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Записи_дня_Спортивные_добавки_ID_добавки",
                        column: x => x.ID_добавки,
                        principalTable: "Спортивные_добавки",
                        principalColumn: "ID_добавки");
                });

            migrationBuilder.CreateTable(
                name: "Упражнения_пункта",
                columns: table => new
                {
                    ID_записи = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_пункта = table.Column<int>(type: "int", nullable: false),
                    ID_упражнения = table.Column<int>(type: "int", nullable: false),
                    Порядок = table.Column<byte>(type: "tinyint", nullable: false),
                    Подходы = table.Column<byte>(type: "tinyint", nullable: false),
                    Повторения_мин = table.Column<short>(type: "smallint", nullable: true),
                    Повторения_макс = table.Column<short>(type: "smallint", nullable: true),
                    Секунд = table.Column<short>(type: "smallint", nullable: true),
                    Отдых_сек = table.Column<short>(type: "smallint", nullable: true),
                    Вес_кг = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Комментарий = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Упражнения_пункта", x => x.ID_записи);
                    table.ForeignKey(
                        name: "FK_Упражнения_пункта_Пункты_программы_ID_пункта",
                        column: x => x.ID_пункта,
                        principalTable: "Пункты_программы",
                        principalColumn: "ID_пункта",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Упражнения_пункта_Упражнения_ID_упражнения",
                        column: x => x.ID_упражнения,
                        principalTable: "Упражнения",
                        principalColumn: "ID_упражнения");
                });

            migrationBuilder.CreateTable(
                name: "Подходы",
                columns: table => new
                {
                    ID_подхода = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_записи_дня = table.Column<long>(type: "bigint", nullable: false),
                    ID_упражнения = table.Column<int>(type: "int", nullable: false),
                    Номер = table.Column<short>(type: "smallint", nullable: false),
                    Вес_кг = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Повторения = table.Column<short>(type: "smallint", nullable: true),
                    Секунд = table.Column<short>(type: "smallint", nullable: true),
                    Выполнено = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Подходы", x => x.ID_подхода);
                    table.ForeignKey(
                        name: "FK_Подходы_Записи_дня_ID_записи_дня",
                        column: x => x.ID_записи_дня,
                        principalTable: "Записи_дня",
                        principalColumn: "ID_записи",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Подходы_Упражнения_ID_упражнения",
                        column: x => x.ID_упражнения,
                        principalTable: "Упражнения",
                        principalColumn: "ID_упражнения");
                });

            migrationBuilder.CreateTable(
                name: "Состав_приёма_пищи",
                columns: table => new
                {
                    ID_строки = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ID_записи = table.Column<long>(type: "bigint", nullable: false),
                    ID_продукта = table.Column<int>(type: "int", nullable: true),
                    Название = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Масса_г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Ккал_на_100г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Белки_на_100г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Жиры_на_100г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Углеводы_на_100г = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Состав_приёма_пищи", x => x.ID_строки);
                    table.ForeignKey(
                        name: "FK_Состав_приёма_пищи_Записи_дня_ID_записи",
                        column: x => x.ID_записи,
                        principalTable: "Записи_дня",
                        principalColumn: "ID_записи",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Состав_приёма_пищи_Продукты_ID_продукта",
                        column: x => x.ID_продукта,
                        principalTable: "Продукты",
                        principalColumn: "ID_продукта");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Внешние_входы_ID_пользователя",
                table: "Внешние_входы",
                column: "ID_пользователя");

            migrationBuilder.CreateIndex(
                name: "IX_Журнал_действий_администратора_Тип_сущности_ID_сущности_Дата_и_время",
                table: "Журнал_действий_администратора",
                columns: new[] { "Тип_сущности", "ID_сущности", "Дата_и_время" });

            migrationBuilder.CreateIndex(
                name: "IX_Журнал_действий_администратора_ID_администратора_Дата_и_время",
                table: "Журнал_действий_администратора",
                columns: new[] { "ID_администратора", "Дата_и_время" });

            migrationBuilder.CreateIndex(
                name: "IX_Замеры_тела_ID_пользователя_Дата_замера",
                table: "Замеры_тела",
                columns: new[] { "ID_пользователя", "Дата_замера" });

            migrationBuilder.CreateIndex(
                name: "IX_Замеры_тела_ID_типа",
                table: "Замеры_тела",
                column: "ID_типа");

            migrationBuilder.CreateIndex(
                name: "IX_Записи_дня_ID_добавки",
                table: "Записи_дня",
                column: "ID_добавки");

            migrationBuilder.CreateIndex(
                name: "IX_Записи_дня_ID_метода_восстановления",
                table: "Записи_дня",
                column: "ID_метода_восстановления");

            migrationBuilder.CreateIndex(
                name: "IX_Записи_дня_ID_пользователя_Начало",
                table: "Записи_дня",
                columns: new[] { "ID_пользователя", "Начало" });

            migrationBuilder.CreateIndex(
                name: "IX_Записи_дня_ID_пункта_программы_Начало",
                table: "Записи_дня",
                columns: new[] { "ID_пункта_программы", "Начало" });

            migrationBuilder.CreateIndex(
                name: "IX_Личные_заметки_ID_пользователя",
                table: "Личные_заметки",
                column: "ID_пользователя");

            migrationBuilder.CreateIndex(
                name: "IX_Методы_восстановления_ID_владельца",
                table: "Методы_восстановления",
                column: "ID_владельца");

            migrationBuilder.CreateIndex(
                name: "IX_Методы_восстановления_ID_источника",
                table: "Методы_восстановления",
                column: "ID_источника");

            migrationBuilder.CreateIndex(
                name: "IX_Подходы_ID_записи_дня",
                table: "Подходы",
                column: "ID_записи_дня");

            migrationBuilder.CreateIndex(
                name: "IX_Подходы_ID_упражнения",
                table: "Подходы",
                column: "ID_упражнения");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Пользователи",
                column: "Нормализованная_электронная_почта");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Пользователи",
                column: "Нормализованное_имя_пользователя",
                unique: true,
                filter: "[Нормализованное_имя_пользователя] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Пользователи_Роли_ID_роли",
                table: "Пользователи_Роли",
                column: "ID_роли");

            migrationBuilder.CreateIndex(
                name: "IX_Пользовательские_форматы_тренировок_ID_формата",
                table: "Пользовательские_форматы_тренировок",
                column: "ID_формата");

            migrationBuilder.CreateIndex(
                name: "IX_Программы_Вид_ID_владельца",
                table: "Программы",
                columns: new[] { "Вид", "ID_владельца" });

            migrationBuilder.CreateIndex(
                name: "IX_Программы_ID_владельца",
                table: "Программы",
                column: "ID_владельца");

            migrationBuilder.CreateIndex(
                name: "IX_Программы_ID_исходной_программы",
                table: "Программы",
                column: "ID_исходной_программы");

            migrationBuilder.CreateIndex(
                name: "IX_Программы_ID_уровня",
                table: "Программы",
                column: "ID_уровня");

            migrationBuilder.CreateIndex(
                name: "IX_Продукты_ID_владельца_Вид",
                table: "Продукты",
                columns: new[] { "ID_владельца", "Вид" });

            migrationBuilder.CreateIndex(
                name: "IX_Продукты_ID_источника",
                table: "Продукты",
                column: "ID_источника");

            migrationBuilder.CreateIndex(
                name: "IX_Продукты_ID_категории",
                table: "Продукты",
                column: "ID_категории");

            migrationBuilder.CreateIndex(
                name: "IX_Профили_ID_активности",
                table: "Профили",
                column: "ID_активности");

            migrationBuilder.CreateIndex(
                name: "IX_Профили_ID_пользователя",
                table: "Профили",
                column: "ID_пользователя",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Профили_ID_уровня_подготовки",
                table: "Профили",
                column: "ID_уровня_подготовки");

            migrationBuilder.CreateIndex(
                name: "IX_Профили_ID_цели",
                table: "Профили",
                column: "ID_цели");

            migrationBuilder.CreateIndex(
                name: "IX_Пункты_программы_ID_добавки",
                table: "Пункты_программы",
                column: "ID_добавки");

            migrationBuilder.CreateIndex(
                name: "IX_Пункты_программы_ID_метода_восстановления",
                table: "Пункты_программы",
                column: "ID_метода_восстановления");

            migrationBuilder.CreateIndex(
                name: "IX_Пункты_программы_ID_программы",
                table: "Пункты_программы",
                column: "ID_программы");

            migrationBuilder.CreateIndex(
                name: "IX_Пункты_программы_ID_продукта",
                table: "Пункты_программы",
                column: "ID_продукта");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Роли",
                column: "Нормализованное_название",
                unique: true,
                filter: "[Нормализованное_название] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Состав_приёма_пищи_ID_записи",
                table: "Состав_приёма_пищи",
                column: "ID_записи");

            migrationBuilder.CreateIndex(
                name: "IX_Состав_приёма_пищи_ID_продукта",
                table: "Состав_приёма_пищи",
                column: "ID_продукта");

            migrationBuilder.CreateIndex(
                name: "IX_Состав_рецепта_ID_продукта",
                table: "Состав_рецепта",
                column: "ID_продукта");

            migrationBuilder.CreateIndex(
                name: "IX_Состав_рецепта_ID_рецепта",
                table: "Состав_рецепта",
                column: "ID_рецепта");

            migrationBuilder.CreateIndex(
                name: "IX_Спортивные_добавки_ID_владельца",
                table: "Спортивные_добавки",
                column: "ID_владельца");

            migrationBuilder.CreateIndex(
                name: "IX_Спортивные_добавки_ID_источника",
                table: "Спортивные_добавки",
                column: "ID_источника");

            migrationBuilder.CreateIndex(
                name: "IX_Спортивные_добавки_ID_категории",
                table: "Спортивные_добавки",
                column: "ID_категории");

            migrationBuilder.CreateIndex(
                name: "IX_Статьи_ID_категории",
                table: "Статьи",
                column: "ID_категории");

            migrationBuilder.CreateIndex(
                name: "IX_Уведомления_ID_пользователя",
                table: "Уведомления",
                column: "ID_пользователя");

            migrationBuilder.CreateIndex(
                name: "IX_Упражнения_ID_владельца",
                table: "Упражнения",
                column: "ID_владельца");

            migrationBuilder.CreateIndex(
                name: "IX_Упражнения_ID_исходного_упражнения",
                table: "Упражнения",
                column: "ID_исходного_упражнения");

            migrationBuilder.CreateIndex(
                name: "IX_Упражнения_ID_категории",
                table: "Упражнения",
                column: "ID_категории");

            migrationBuilder.CreateIndex(
                name: "IX_Упражнения_Мышцы_ID_группы",
                table: "Упражнения_Мышцы",
                column: "ID_группы");

            migrationBuilder.CreateIndex(
                name: "IX_Упражнения_пункта_ID_пункта",
                table: "Упражнения_пункта",
                column: "ID_пункта");

            migrationBuilder.CreateIndex(
                name: "IX_Упражнения_пункта_ID_упражнения",
                table: "Упражнения_пункта",
                column: "ID_упражнения");

            migrationBuilder.CreateIndex(
                name: "IX_Утверждения_пользователей_ID_пользователя",
                table: "Утверждения_пользователей",
                column: "ID_пользователя");

            migrationBuilder.CreateIndex(
                name: "IX_Утверждения_ролей_ID_роли",
                table: "Утверждения_ролей",
                column: "ID_роли");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Внешние_входы");

            migrationBuilder.DropTable(
                name: "Журнал_действий_администратора");

            migrationBuilder.DropTable(
                name: "Замеры_тела");

            migrationBuilder.DropTable(
                name: "Личные_заметки");

            migrationBuilder.DropTable(
                name: "Настройки_системы");

            migrationBuilder.DropTable(
                name: "Подходы");

            migrationBuilder.DropTable(
                name: "Пользователи_Роли");

            migrationBuilder.DropTable(
                name: "Пользовательские_форматы_тренировок");

            migrationBuilder.DropTable(
                name: "Профили");

            migrationBuilder.DropTable(
                name: "Состав_приёма_пищи");

            migrationBuilder.DropTable(
                name: "Состав_рецепта");

            migrationBuilder.DropTable(
                name: "Статьи");

            migrationBuilder.DropTable(
                name: "Токены_пользователей");

            migrationBuilder.DropTable(
                name: "Уведомления");

            migrationBuilder.DropTable(
                name: "Упражнения_Мышцы");

            migrationBuilder.DropTable(
                name: "Упражнения_пункта");

            migrationBuilder.DropTable(
                name: "Утверждения_пользователей");

            migrationBuilder.DropTable(
                name: "Утверждения_ролей");

            migrationBuilder.DropTable(
                name: "Типы_замеров");

            migrationBuilder.DropTable(
                name: "Форматы_тренировок");

            migrationBuilder.DropTable(
                name: "Уровни_активности");

            migrationBuilder.DropTable(
                name: "Цели");

            migrationBuilder.DropTable(
                name: "Записи_дня");

            migrationBuilder.DropTable(
                name: "Группы_мышц");

            migrationBuilder.DropTable(
                name: "Упражнения");

            migrationBuilder.DropTable(
                name: "Роли");

            migrationBuilder.DropTable(
                name: "Пункты_программы");

            migrationBuilder.DropTable(
                name: "Методы_восстановления");

            migrationBuilder.DropTable(
                name: "Программы");

            migrationBuilder.DropTable(
                name: "Продукты");

            migrationBuilder.DropTable(
                name: "Спортивные_добавки");

            migrationBuilder.DropTable(
                name: "Уровни_подготовки");

            migrationBuilder.DropTable(
                name: "Категории");

            migrationBuilder.DropTable(
                name: "Пользователи");
        }
    }
}
