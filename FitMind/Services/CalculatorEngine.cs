using System.Globalization;
namespace FitMind.Services;
public record CalcField(string Key,string Label,double Default,double Min,double Max);
public record CalcDefinition(string Key,string Title,string Category,string Description,string Formula,CalcField[] Fields);
public class CalculatorPage
{
 public string Calculator { get; set; } = "BMI";
 public Dictionary<string,decimal> Values { get; set; } = new();
 public Dictionary<string,string> Results { get; set; } = new();
 public List<FitMind.Models.PersonalNote> History { get; set; } = [];
 public CalcDefinition Definition => CalculatorEngine.All.First(x=>x.Key==Calculator);
}
public static class CalculatorEngine
{
 public static readonly CalcDefinition[] All = {
  new("BMI","ИМТ","Тело и состав тела","Индекс массы тела: соотношение веса и роста.","ИМТ = вес / рост_м². Рост в метрах.", new CalcField[]{new CalcField("w","Вес, кг",70,1,500),new CalcField("h","Рост, см",175,50,250)}),
  new("RANGE","Нормальный диапазон веса","Тело и состав тела","Примерный диапазон веса при ИМТ 18.5–24.9.","Мин. вес = 18.5 × рост_м². Макс. вес = 24.9 × рост_м².", new CalcField[]{new CalcField("h","Рост, см",175,50,250)}),
  new("WHTR","Талия/рост (WHtR)","Тело и состав тела","Соотношение окружности талии к росту.","WHtR = талия / рост. Значения в одинаковых единицах, обычно см.", new CalcField[]{new CalcField("waist","Талия, см",80,1,300),new CalcField("h","Рост, см",175,50,250)}),
  new("WHR","Талия/бёдра (WHR)","Тело и состав тела","Соотношение талии и бёдер.","WHR = талия / бёдра.", new CalcField[]{new CalcField("waist","Талия, см",80,1,300),new CalcField("hip","Бёдра, см",95,1,300)}),
  new("FAT","Процент жира по Navy Method","Тело и состав тела","Оценка процента жира по окружностям тела.","Мужчины: % = 495 / (1.0324 − 0.19077 × log10(талия − шея) + 0.15456 × log10(рост)) − 450. Женщины: % = 495 / (1.29579 − 0.35004 × log10(талия + бёдра − шея) + 0.22100 × log10(рост)) − 450. Все окружности в см.", new CalcField[]{new CalcField("female","Пол: 0 — мужской, 1 — женский",0,0,1),new CalcField("h","Рост, см",175,50,250),new CalcField("waist","Талия, см",80,1,300),new CalcField("neck","Шея, см",35,1,100),new CalcField("hip","Бёдра, см",95,1,300)}),
  new("FATMASS","Жировая масса","Тело и состав тела","Количество жира в килограммах.","Жировая масса = вес × процент жира / 100.", new CalcField[]{new CalcField("w","Вес, кг",70,1,500),new CalcField("fat","Процент жира",20,0,80)}),
  new("LEAN","Безжировая масса","Тело и состав тела","Масса тела без жира.","Безжировая масса = вес − жировая масса; или вес × (1 − процент жира / 100).", new CalcField[]{new CalcField("w","Вес, кг",70,1,500),new CalcField("fat","Процент жира",20,0,80)}),
  new("FFMI","FFMI","Тело и состав тела","Индекс безжировой массы относительно роста.","FFMI = безжировая масса / рост_м². Рост в метрах.", new CalcField[]{new CalcField("lean","Безжировая масса, кг",56,1,300),new CalcField("h","Рост, см",175,50,250)}),
  new("BMR","BMR","Питание и калории","Базовый обмен веществ: примерный расход калорий в покое.","Mifflin-St Jeor. Мужчины: BMR = 10 × вес + 6.25 × рост − 5 × возраст + 5. Женщины: BMR = 10 × вес + 6.25 × рост − 5 × возраст − 161.", new CalcField[]{new CalcField("female","Пол: 0 — мужской, 1 — женский",0,0,1),new CalcField("age","Возраст, полных лет",25,18,100),new CalcField("h","Рост, см",175,50,250),new CalcField("w","Вес, кг",70,1,500)}),
  new("TDEE","TDEE","Питание и калории","Суточная норма калорий с учётом активности.","TDEE = BMR × коэффициент активности. Коэффициенты: 1.2, 1.375, 1.55, 1.725, 1.9.", new CalcField[]{new CalcField("bmr","BMR, ккал/сутки",1600,100,10000),new CalcField("activity","Активность: 1.2 / 1.375 / 1.55 / 1.725 / 1.9",1.55,1.2,1.9)}),
  new("CALORIES","Калории под цель","Питание и калории","Калории для похудения, поддержания или набора.","Поддержание = TDEE. Похудение = TDEE × (1 − дефицит%). Набор = TDEE × (1 + профицит%).", new CalcField[]{new CalcField("tdee","TDEE, ккал/сутки",2400,100,10000),new CalcField("delta","Изменение калорий, % (дефицит: отрицательное)",-10,-30,30)}),
  new("MACROS","БЖУ по процентам","Питание и калории","Распределение калорий на белки, жиры и углеводы.","Белки г = ккал × % белков / 100 / 4. Жиры г = ккал × % жиров / 100 / 9. Углеводы г = ккал × % углеводов / 100 / 4.", new CalcField[]{new CalcField("kcal","Калории, ккал",2200,1,10000),new CalcField("pPct","Белки, %",25,0,100),new CalcField("fPct","Жиры, %",30,0,100),new CalcField("cPct","Углеводы, %",45,0,100)}),
  new("PROTEIN","Белок по весу","Питание и калории","Расчёт дневной нормы белка по массе тела.","Белок г = вес × коэффициент. Для тренирующихся обычно 1.4–2.0 г/кг, для дефицита часто 1.8–2.2 г/кг.", new CalcField[]{new CalcField("w","Вес, кг",70,1,500),new CalcField("pFactor","Белок, г/кг",1.8,0.1,4)}),
  new("FATS","Жиры по весу","Питание и калории","Расчёт примерной нормы жиров.","Жиры г = вес × 0.8–1.0. Альтернатива: жиры г = ккал × % жиров / 100 / 9.", new CalcField[]{new CalcField("w","Вес, кг",70,1,500),new CalcField("fFactor","Жиры, г/кг",0.9,0.1,3)}),
  new("CARBS","Углеводы по остатку","Питание и калории","Расчёт углеводов после белков и жиров.","Углеводы г = (ккал − белки × 4 − жиры × 9) / 4.", new CalcField[]{new CalcField("kcal","Калории, ккал",2200,1,10000),new CalcField("protein","Белки, г",140,0,1000),new CalcField("fats","Жиры, г",70,0,1000)}),
  new("WATER","Вода","Питание и калории","Примерная дневная цель воды.","Вода мл = вес × 30–35. В день тренировки можно добавить 500–1000 мл.", new CalcField[]{new CalcField("w","Вес, кг",70,1,500),new CalcField("water","Вода, мл/кг",35,30,35),new CalcField("extra","Дополнительно, мл",0,0,2000)}),
  new("FIBER","Клетчатка","Питание и калории","Ориентир по клетчатке в питании.","Клетчатка г = ккал / 1000 × 14.", new CalcField[]{new CalcField("kcal","Калории, ккал",2200,1,10000)}),
  new("1RM","1ПМ Epley","Тренировки и сила","Расчёт примерного одноповторного максимума.","1ПМ = вес × (1 + повторения / 30).", new CalcField[]{new CalcField("load","Рабочий вес, кг",60,0,1500),new CalcField("reps","Повторения",10,1,12)}),
  new("BRZYCKI","1ПМ Brzycki","Тренировки и сила","Альтернативная формула одноповторного максимума.","1ПМ = вес × 36 / (37 − повторения). Лучше применять примерно до 10–12 повторений.", new CalcField[]{new CalcField("load","Рабочий вес, кг",60,0,1500),new CalcField("reps","Повторения",10,1,12)}),
  new("WORKING","Рабочий вес от 1ПМ","Тренировки и сила","Подбор веса по проценту от максимума.","Рабочий вес = 1ПМ × процент / 100.", new CalcField[]{new CalcField("rm","1ПМ, кг",100,1,1500),new CalcField("pct","Нагрузка, %",80,1,100)}),
  new("VOLUME","Тренировочный объём упражнения","Тренировки и сила","Общая нагрузка в одном упражнении.","Объём = подходы × повторения × вес.", new CalcField[]{new CalcField("sets","Подходы",3,1,100),new CalcField("reps","Повторения",10,1,12),new CalcField("load","Рабочий вес, кг",60,0,1500)}),
  new("TOTAL","Общий объём тренировки","Тренировки и сила","Суммарная нагрузка за тренировку.","Общий объём = сумма объёма всех упражнений.", new CalcField[]{new CalcField("v1","Объём упражнения 1, кг",1000,0,1000000),new CalcField("v2","Объём упражнения 2, кг",1000,0,1000000),new CalcField("v3","Объём остальных упражнений, кг",0,0,1000000)}),
  new("AVERAGE","Средний рабочий вес","Тренировки и сила","Средний вес на одно повторение.","Средний вес = общий объём / общее количество повторений.", new CalcField[]{new CalcField("volume","Общий объём, кг",2000,0,1000000),new CalcField("totalReps","Общее количество повторений",40,1,100000)}),
  new("DENSITY","Плотность тренировки","Тренировки и сила","Нагрузка относительно времени тренировки.","Плотность = общий объём / длительность в минутах.", new CalcField[]{new CalcField("volume","Общий объём, кг",2000,0,1000000),new CalcField("minutes","Длительность, мин",30,0.01,1440)}),
  new("MAXHR","Максимальный пульс","Кардио и бег","Приближённая оценка максимальной ЧСС.","HRmax = 220 − возраст.", new CalcField[]{new CalcField("age","Возраст, полных лет",25,18,100)}),
  new("ZONES","Пульсовые зоны","Кардио и бег","Диапазоны пульса для разной интенсивности.","Нижняя граница = HRmax × нижний %. Верхняя граница = HRmax × верхний %. Например 50–70%.", new CalcField[]{new CalcField("maxHr","Максимальный пульс, уд/мин",195,1,250),new CalcField("low","Нижняя граница, %",50,1,100),new CalcField("high","Верхняя граница, %",70,1,100)}),
  new("HEART","Пульсовые зоны Карвонена","Кардио и бег","Зоны пульса с учётом пульса покоя.","HRmax = 220 − возраст. Резерв = HRmax − пульс покоя. Целевая ЧСС = резерв × интенсивность + пульс покоя.", new CalcField[]{new CalcField("age","Возраст, полных лет",25,18,100),new CalcField("rest","Пульс покоя, уд/мин",60,30,180),new CalcField("intensity","Интенсивность, %",60,1,100)}),
  new("MET","Калории за активность по MET","Кардио и бег","Оценка расхода калорий за тренировку.","ккал = MET × 3.5 × вес / 200 × минуты.", new CalcField[]{new CalcField("met","MET активности",5,0.1,25),new CalcField("w","Вес, кг",70,1,500),new CalcField("minutes","Длительность, мин",30,0.01,1440)}),
  new("PACE","Темп бега","Кардио и бег","Время на 1 км.","Темп = время / дистанция. Например 30 минут / 5 км = 6:00 мин/км.", new CalcField[]{new CalcField("km","Дистанция, км",5,0.001,1000),new CalcField("minutes","Длительность, мин",30,0.01,1440)}),
  new("SPEED","Скорость бега","Кардио и бег","Средняя скорость движения.","Скорость км/ч = дистанция км / время в часах.", new CalcField[]{new CalcField("km","Дистанция, км",5,0.001,1000),new CalcField("minutes","Длительность, мин",30,0.01,1440)}),
  new("STEPS","Дистанция по шагам","Кардио и бег","Примерная дистанция по количеству шагов.","Дистанция м = шаги × длина шага м.", new CalcField[]{new CalcField("steps","Количество шагов",10000,0,1000000),new CalcField("stride","Длина шага, м",0.7,0.01,2)}),
  new("STEPCAL","Калории по шагам","Кардио и бег","Грубая оценка расхода калорий при ходьбе.","ккал ≈ вес кг × дистанция км × 0.5–0.8. Точнее использовать MET.", new CalcField[]{new CalcField("w","Вес, кг",70,1,500),new CalcField("km","Дистанция, км",5,0.001,1000),new CalcField("stepFactor","Коэффициент, ккал/(кг·км)",0.6,0.5,0.8)}),
  new("SLEEP","Длительность сна","Сон и восстановление","Сколько часов пользователь спал.","Сон = время пробуждения − время засыпания. Если переход через полночь, добавить 24 часа.", new CalcField[]{new CalcField("start","Засыпание: час суток (23.5 = 23:30)",23,0,23.999),new CalcField("end","Пробуждение: час суток (7.5 = 07:30)",7,0,23.999)}),
  new("DEFICIT","Дефицит сна","Сон и восстановление","Недобор сна относительно цели.","Дефицит = целевой сон − фактический сон. Если результат меньше 0, дефицит = 0.", new CalcField[]{new CalcField("actual","Фактический сон, ч",7,0,24),new CalcField("target","Целевой сон, ч",8,0,24)}),
  new("AVGSLEEP","Средний сон","Сон и восстановление","Средняя длительность сна за период.","Средний сон = сумма часов сна / количество дней.", new CalcField[]{new CalcField("hours","Сумма часов сна",49,0,10000),new CalcField("days","Количество дней",7,1,365)}),
 };
 public static void Calculate(CalculatorPage vm)
 {
  foreach(var f in vm.Definition.Fields) {
   if(!vm.Values.TryGetValue(f.Key,out var value) || (double)value<f.Min || (double)value>f.Max) throw new ArgumentException($"{f.Label}: допустимо от {f.Min} до {f.Max}.");
  }
  if(vm.Values.TryGetValue("female",out var femaleValue)&&femaleValue!=0&&femaleValue!=1)throw new ArgumentException("Пол: укажите 0 (мужской) или 1 (женский).");
  double V(string k)=>(double)vm.Values.GetValueOrDefault(k);
  void R(string k,double v,string unit){if(!double.IsFinite(v))throw new ArgumentException("Проверьте введённые данные.");vm.Results[k]=$"{v.ToString("0.##",CultureInfo.GetCultureInfo("ru-RU"))} {unit}";}
  switch(vm.Calculator){
   case "BMI": {double w=V("w");double h=V("h");R("ИМТ",w/(h/100*h/100),"");break;}
   case "RANGE": {double h=V("h");R("Минимум",18.5*h*h/10000,"кг");R("Максимум",24.9*h*h/10000,"кг");break;}
   case "WHTR": {double waist=V("waist");double h=V("h");R("Талия/рост",waist/h,"");break;}
   case "WHR": {double waist=V("waist");double hip=V("hip");R("Талия/бёдра",waist/hip,"");break;}
   case "FAT": {double female=V("female");double h=V("h");double waist=V("waist");double neck=V("neck");double hip=V("hip");var a=female==1?waist+hip-neck:waist-neck; if(a<=0)throw new ArgumentException("Сумма обхватов должна превышать обхват шеи."); var b=female==1?1.29579-0.35004*Math.Log10(a)+0.22100*Math.Log10(h):1.0324-0.19077*Math.Log10(a)+0.15456*Math.Log10(h);var fatResult=495/b-450;if(fatResult<0||fatResult>80)throw new ArgumentException("Проверьте замеры: результат вне допустимого диапазона.");R("Оценка жира",fatResult,"%");break;}
   case "FATMASS": {double w=V("w");double fat=V("fat");R("Жировая масса",w*fat/100,"кг");break;}
   case "LEAN": {double w=V("w");double fat=V("fat");R("Безжировая масса",w*(1-fat/100),"кг");break;}
   case "FFMI": {double lean=V("lean");double h=V("h");R("FFMI",lean/(h*h/10000),"");break;}
   case "BMR": {double female=V("female");double age=V("age");double h=V("h");double w=V("w");R("BMR",10*w+6.25*h-5*age+(female==1?-161:5),"ккал/сутки");break;}
   case "TDEE": {double bmr=V("bmr");double activity=V("activity");R("TDEE",bmr*activity,"ккал/сутки");break;}
   case "CALORIES": {double tdee=V("tdee");double delta=V("delta");R("Калории под цель",tdee*(1+delta/100),"ккал/сутки");break;}
   case "MACROS": {double kcal=V("kcal");double pPct=V("pPct");double fPct=V("fPct");double cPct=V("cPct");if(Math.Abs(pPct+fPct+cPct-100)>.001)throw new ArgumentException("Сумма процентов БЖУ должна равняться 100.");R("Белки",kcal*pPct/400,"г");R("Жиры",kcal*fPct/900,"г");R("Углеводы",kcal*cPct/400,"г");break;}
   case "PROTEIN": {double w=V("w");double pFactor=V("pFactor");R("Белок",w*pFactor,"г");break;}
   case "FATS": {double w=V("w");double fFactor=V("fFactor");R("Жиры",w*fFactor,"г");break;}
   case "CARBS": {double kcal=V("kcal");double protein=V("protein");double fats=V("fats");var remain=kcal-protein*4-fats*9;if(remain<0)throw new ArgumentException("Белки и жиры превышают заданные калории.");R("Углеводы",remain/4,"г");break;}
   case "WATER": {double w=V("w");double water=V("water");double extra=V("extra");R("Вода",(w*water+extra)/1000,"л");break;}
   case "FIBER": {double kcal=V("kcal");R("Клетчатка",kcal/1000*14,"г");break;}
   case "1RM": {double load=V("load");double reps=V("reps");R("1ПМ Эпли",reps==1?load:load*(1+reps/30),"кг");break;}
   case "BRZYCKI": {double load=V("load");double reps=V("reps");R("1ПМ Бжицки",load*36/(37-reps),"кг");break;}
   case "WORKING": {double rm=V("rm");double pct=V("pct");R("Рабочий вес",rm*pct/100,"кг");break;}
   case "VOLUME": {double sets=V("sets");double reps=V("reps");double load=V("load");R("Объём",sets*reps*load,"кг");break;}
   case "TOTAL": {double v1=V("v1");double v2=V("v2");double v3=V("v3");R("Общий объём",v1+v2+v3,"кг");break;}
   case "AVERAGE": {double volume=V("volume");double totalReps=V("totalReps");R("Средний вес",volume/totalReps,"кг");break;}
   case "DENSITY": {double volume=V("volume");double minutes=V("minutes");R("Плотность",volume/minutes,"кг/мин");break;}
   case "MAXHR": {double age=V("age");R("HRmax",220-age,"уд/мин");break;}
   case "ZONES": {double maxHr=V("maxHr");double low=V("low");double high=V("high");if(low>high)throw new ArgumentException("Нижняя граница не может быть выше верхней.");R("Нижняя граница",maxHr*low/100,"уд/мин");R("Верхняя граница",maxHr*high/100,"уд/мин");break;}
   case "HEART": {double age=V("age");double rest=V("rest");double intensity=V("intensity");if(rest>=220-age)throw new ArgumentException("Пульс покоя должен быть ниже максимального.");R("Целевая ЧСС",(220-age-rest)*intensity/100+rest,"уд/мин");break;}
   case "MET": {double met=V("met");double w=V("w");double minutes=V("minutes");R("Расход энергии",met*3.5*w/200*minutes,"ккал");break;}
   case "PACE": {double km=V("km");double minutes=V("minutes");var seconds=(int)Math.Round(minutes/km*60);vm.Results["Темп"]=$"{seconds/60}:{seconds%60:00} мин/км";break;}
   case "SPEED": {double km=V("km");double minutes=V("minutes");R("Скорость",km/(minutes/60),"км/ч");break;}
   case "STEPS": {double steps=V("steps");double stride=V("stride");R("Дистанция",steps*stride/1000,"км");break;}
   case "STEPCAL": {double w=V("w");double km=V("km");double stepFactor=V("stepFactor");R("Оценка расхода",w*km*stepFactor,"ккал");break;}
   case "SLEEP": {double start=V("start");double end=V("end");R("Продолжительность сна",end>=start?end-start:end-start+24,"ч");break;}
   case "DEFICIT": {double actual=V("actual");double target=V("target");R("Дефицит сна",Math.Max(0,target-actual),"ч");break;}
   case "AVGSLEEP": {double hours=V("hours");double days=V("days");if(hours/days>24)throw new ArgumentException("Средний сон не может превышать 24 часа в сутки.");R("Средний сон",hours/days,"ч");break;}
  }
 }
}
