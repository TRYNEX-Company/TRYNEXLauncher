# TRYNEX Launcher

Новый нативный лаунчер экосистемы TRYNEX для Windows. Проект переписывается с чистой архитектурой и принципом «сначала безопасность, потом удобство».

## Текущее состояние

Версия `0.3.0-preview.11` содержит:

- современную WPF-оболочку на .NET 10;
- рабочую навигацию между главной, библиотекой, загрузками, сообществом, мессенджером и настройками;
- локальные настройки в `%AppData%\TRYNEX\settings.json` с атомарной записью;
- проверку путей манифеста против path traversal и NTFS alternate data streams;
- проверку размера и SHA-256 файлов без удаления или изменения пользовательских данных;
- отдельный `Trynex.Bootstrapper` с версионными каталогами и автоматическим откатом;
- подписанный ECDSA P-256 манифест релиза и строгую проверку R2 object path;
- HTTPS-загрузку пакета с продолжением после обрыва и ограничением подписанного размера;
- безопасную распаковку ZIP без path traversal, дубликатов и символических ссылок;
- `Trynex.ReleaseTool` для создания ключей и подписанных манифестов;
- подключённый Cloudflare R2 preview-канал с постоянным публичным ключом проверки;
- вход через единый TRYNEX ID в системном браузере по OAuth 2.1 Authorization Code + PKCE;
- зашифрованное Windows DPAPI хранение сессии без пароля и client secret в лаунчере;
- реальные логотипы TRYNEX и MR Project, обновлённую карточку Arma Reforger и тёмный выбор языка;
- 94 автоматических теста для основной логики, инфраструктуры, OAuth/PKCE, MVVM и загрузки WPF-оболочки.

Preview-обновления через R2 и клиент единой авторизации подключены. Постоянная точка входа `TRYNEX.exe` показывает проверку, загрузку, установку и безопасный запуск. Производственный вход заработает после развёртывания `id.trynex.dev` и его D1-базы. Установка модов, запуск игр и мессенджер пока остаются следующими этапами.

## Запуск

Требования:

- Windows 11;
- Visual Studio 2026 с нагрузкой `.NET desktop development`;
- .NET SDK 10.

Откройте `TRYNEX.slnx`, назначьте `Trynex.Launcher` стартовым проектом и запустите через `F5`.

Проверка из терминала:

```powershell
dotnet build .\TRYNEX.slnx --configuration Debug
dotnet test .\TRYNEX.slnx --configuration Debug
```

## Структура

```text
src/
  Trynex.Bootstrapper/    постоянный TRYNEX.exe: окно обновления, запуск и rollback
  Trynex.Core/            доменные модели, правила безопасности, интерфейсы
  Trynex.Infrastructure/  файловая система, SHA-256, локальные настройки
  Trynex.Launcher/        WPF, MVVM, страницы и композиция приложения
tests/
  Trynex.Core.Tests/
  Trynex.Infrastructure.Tests/
docs/
  ARCHITECTURE.md
  ROADMAP.md
  SECURITY.md
  TRYNEX_ID.md
tools/
  Trynex.ReleaseTool/     генерация ключей и подписанного manifest.json
  New-LauncherRelease.ps1 проверка, сборка и подготовка двух файлов для R2
```

Подробности находятся в каталоге `docs`.
