# Abacus Kids Calculator

Обучающее приложение для детей 4–9 лет: сложение и вычитание через визуальные счёты. Число показано бусинами на спицах (единицы, десятки, сотни, тысячи); ребёнок меняет разряды кнопками и проверяет ответ.

Примеры вида `a + b` и `a − b` генерируются автоматически. Сложение не выходит за текущий уровень (10, затем 20), вычитание всегда даёт неотрицательный результат. После трёх верных ответов подряд открывается уровень до 20; две ошибки на этом уровне возвращают к 10.

Стек: .NET 10, .NET MAUI, MVVM. Логика в `src/KidAbacusCalculator.Core`, UI в `src/KidAbacusCalculator`. Требования к продукту — в `PRD.md`.

## Релиз

Нужны SDK .NET 10 и workload MAUI (`dotnet workload install maui-windows` и/или `maui-android`). Для Android ещё JDK 17.

Проще всего поставить тег `v1.0.0`: workflow `.github/workflows/release.yml` соберёт оба артефакта.

Локально, из корня репозитория.

**Windows** (собирать на Windows). Копировать всю папку `artifacts/windows`, запускать `KidAbacusCalculator.exe`:

```
dotnet publish src/KidAbacusCalculator/KidAbacusCalculator.csproj -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained -o artifacts/windows
```

**Android.** APK ищите в `artifacts/android` или в `src/KidAbacusCalculator/bin`. Подпись — как в `release.yml`; ключ CI только для установки на устройство, не для магазина:

```
dotnet publish src/KidAbacusCalculator/KidAbacusCalculator.csproj -c Release -f net10.0-android -o artifacts/android
```

## Тесты

```
dotnet run --project tests/KidAbacusCalculator.Tests/KidAbacusCalculator.Tests.csproj -c Release
```
