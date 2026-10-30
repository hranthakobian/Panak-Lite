# Panak
### *Android-Style Popup Folders and Widgets for Windows*

<p align="center">
  <img src="https://jveaaytpcviwuvpgdvxl.supabase.co/storage/v1/object/public/gallery/1791793955684_Group_389.png" alt="Panak Logo" width="512" />
</p>

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/R0R828IT50)

---

**Panak** is a modern, smooth, and lightweight utility for Windows (C# / .NET 8 WPF) that brings the convenience and aesthetic of **Android OS / Material You Folder Widgets** to the Windows desktop.

Instead of opening cumbersome, classic desktop folders and cluttering your screen with countless icons, Panak allows you to instantly open frosted-glass popup widgets featuring dynamic preview icons, quick shortcuts, fluid animations, and complete Windows Explorer integration.

---

## Table of Contents

- [Key Features Overview](#key-features-overview)
- [Smooth Scale & Morph Animation](#smooth-scale--morph-animation)
- [Dynamic 2x2 Icon Preview Engine](#dynamic-2x2-icon-preview-engine)
- [Material 3 Expressive Shapes & Pastel Colors](#material-3-expressive-shapes--pastel-colors)
- [Frosted Glass / Acrylic UI](#frosted-glass--acrylic-ui)
- [Subfolders & Breadcrumbs Navigation](#subfolders--breadcrumbs-navigation)
- [Drag & Drop and Automatic File Merging](#drag--drop-and-automatic-file-merging)
- [Instant Gestures & Shortcuts](#instant-gestures--shortcuts)
- [Windows Explorer & Context Menu Integration](#windows-explorer--context-menu-integration)
- [System Tray & Settings Dashboard](#system-tray--settings-dashboard)
- [Multi-Language Localization](#multi-language-localization)
- [GitHub Automatic Updates](#github-automatic-updates)
- [Installation & Uninstallation](#installation--uninstallation)
- [Command Line Interface (CLI)](#command-line-interface-cli)
- [Project Build & Architecture](#project-build--architecture)
- [Support](#support)
- [License](#license)

---

## Key Features Overview

| Feature | Description |
| :--- | :--- |
| **Scale & Morph Animation** | Folder smoothly opens directly from the icon’s screen coordinates and collapses back into the icon upon exit. |
| **Dynamic 2x2 Icon Engine** | Folder icon in Windows Explorer displays a 2x2 live preview of the first 4 files inside (bypassing Windows shell cache). |
| **Material 3 Expressive Shapes** | 5 adaptive geometric shapes: Squircle, Circle, Clover, Scallop, and Arch. |
| **Pastel Colors & Hue** | 8 preset pastel swatches + 360° continuous Hue slider + opacity settings (20% – 100%). |
| **Frosted Glass / Acrylic Backdrop** | Smooth Gaussian Blur frosted glass effect with Light, Dark, and Windows-synced Auto modes. |
| **Subfolder Navigation** | Browse nested folders directly inside the popup with back navigation (`←` / `Backspace`) and breadcrumbs. |
| **Drag-to-Combine Files** | Dragging one file onto another automatically creates a new Panak folder (powered by COM Shell Extension). |
| **Right Double-Click Open** | Double right-click any folder in Explorer or Desktop to instantly open it with Panak. |
| **Quick Look (`Space` Key)** | Press `Space` on any selected folder in Explorer to instantly preview it; press `Space` or `Esc` to close. |
| **Focus Mode** | Softly dims the desktop backdrop when a folder opens, focusing full attention on your files. |
| **Folder Locking** | Prevents accidental file movement or Drag & Drop operations. |
| **7 Languages Supported** | Armenian, English, Russian, French, Spanish, Chinese, and Arabic. |
| **Zero-Admin Installation** | Installs cleanly to `%LocalAppData%\Programs\Panak` without requiring UAC administrator rights. |
| **GitHub Auto-Updates** | Checks GitHub releases in the background and updates the application with a single click. |

---

## Smooth Scale & Morph Animation

Panak accurately replicates the renowned folder animations of Android OS:
- **Coordinate-Aware Scaling**: The popup smoothly scales out directly from the exact screen position of the icon you clicked (on the Desktop or in File Explorer), transforming into a modern Popup card.
- **Center Opening Option**: Can be configured in Settings to always open in the center of the active monitor.
- **Fluid Collapse**: Pressing `Esc`, clicking the dimming backdrop, or launching a file smoothly shrinks the card back into its originating icon.

---

## Dynamic 2x2 Icon Preview Engine

Standard Windows folders look identical and plain. Panak transforms them:
- **2x2 Grid Live Preview**: Generates a high-quality icon reflecting miniature previews of the first 4 files inside the folder (Android Grid style).
- **Windows Cache-Busting Mechanism**: Windows Explorer aggressively caches folder icons. Panak implements sequential icon slot rotation (`panak_icon_{N}.ico` in `desktop.ini`), forcing Windows Shell to refresh immediately whenever items are added or removed.
- **Multi-Resolution ICO Files**: Icons are generated at all standard sizes (256×256, 48×48, 32×32, 16×16) to ensure razor-sharp rendering at any Explorer view scale.

---

## Material 3 Expressive Shapes & Pastel Colors

Every Panak folder can have its own distinct personality:

### 5 Adaptive Geometric Shapes
- **Squircle**: Contemporary smooth-cornered square (superellipse).
- **Circle**: Perfectly rounded circle.
- **Clover**: Playful 4-petal clover flower.
- **Scallop**: Wavy scalloped petal contour.
- **Arch**: Modern architectural curve.

### Color & Transparency Controls
- **8 Preset Pastel Swatches**: Peach, Mint, Cyan, Sky Blue, Lavender, Lilac, Rose, and Butter Yellow.
- **360° Hue Slider**: Fine-tune any custom pastel shade.
- **Opacity Slider**: Adjust background density from 20% translucency to 100% solid color.
- **Inline Renaming**: Double-click the header title or click the ✏️ pencil button to rename the folder instantly.

---

## Frosted Glass / Acrylic UI

Crafted according to modern Fluent and Material 3 design systems:
- **Real-Time Gaussian Blur**: Desktop wallpaper is smoothly blurred directly behind the open card.
- **Theme Options**:
  - **Light Mode**: Crisp, bright frosted card with dark typography.
  - **Dark Mode**: Deep slate acrylic card with radiant accent highlights.
  - **Auto (System Sync)**: Automatically matches your current Windows light/dark theme preference.
- **Grid Customization**: Configure the folder grid to show between **3 and 8 columns** per row (default: 5).

---

## Subfolders & Breadcrumbs Navigation

Panak is not limited to root-level files:
- **Nested Folder Browsing**: Clicking any subfolder inside the popup opens it immediately in the same window.
- **Breadcrumbs Path Bar**: Easily track your directory path and click parent folders to navigate back.
- **Back Navigation**: Click the `←` button or press `Backspace`.
- **Item Count Badge**: Header badge indicates the total number of items in the folder.

---

## Drag & Drop and Automatic File Merging

- **Drop In**: Drag any files or folders from Desktop, Explorer, or browsers and drop them directly into the Panak popup.
- **Drag Out**: Drag files out of the popup onto the Desktop, other folders, or other applications.
- **File-on-File Merge (Android / iOS Style)**:
  - Drag one file and drop it directly onto another file on the Desktop.
  - Panak's COM Shell Extension (`Panak.ShellExt.dll`) prompts to combine both files into a brand-new Panak folder.

---

## Instant Gestures & Shortcuts

| Gesture / Shortcut | Target | Action |
| :--- | :--- | :--- |
| **Right Double-Click** 🖱️ | Any Folder | Instantly opens any Windows folder as a Panak Popup widget. |
| **Space** ␣ *(Quick Look)* | Folder in Explorer | Press `Space` on a folder to preview its contents; press `Space` or `Esc` to close. |
| **Double-Click Title** ✏️ | Popup Header | Activates inline folder renaming. |
| **Backspace** ⌫ | Inside Subfolder | Navigates back to the parent folder. |
| **Escape** `Esc` | Active Popup | Smoothly closes the Panak window. |
| **Click on Backdrop** | Dimmed Desktop Backdrop | Smoothly closes the open folder. |

---

## Windows Explorer & Context Menu Integration

### Windows Context Menu Actions
- **Right-click file(s)** ➔ `«Create Android-style Panak»`: Packs selected files into a new Panak folder.
- **Right-click folder** ➔ `«Open with Panak»`: Opens the folder as a Panak popup widget.
- **Right-click folder** ➔ `«Convert to Panak Folder»`: Converts a regular folder into a Panak folder with 2x2 preview icon.
- **Right-click folder** ➔ `«Revert to Regular Folder»`: Removes Panak settings and restores normal folder appearance.
- **Right-click empty space** ➔ `«New Panak Folder»`: Creates a new empty Panak folder.

### Actions Inside Popup
- **Open**: Launches the file with default application or navigates into subfolder.
- **Open in Windows Explorer**: Reveals the file in native Explorer.
- **Remove from Folder**: Moves the file out of the folder.
- **Delete**: Removes the file.
- **Properties**: Displays the native Windows file properties dialog.

---

## System Tray & Settings Dashboard

Panak runs quietly in the Windows notification area (System Tray) with minimal resource usage:
- **Left-click tray icon**: Opens the Settings dashboard.
- **Right-click tray menu**:
  - **Settings**: System configuration window.
  - **New Panak Folder**: Creates a new folder on the Desktop.
  - **Support Project**: Opens Ko-fi support page.
  - **Check for Updates**: Checks GitHub releases.
  - **Exit**: Closes the application.

### Settings Sections
1. **General**: Language switcher, Run on Windows startup (AutoStart), Ko-fi donation link.
2. **Appearance**: Grid column count (3–8), Theme (Light/Dark/Auto), Default shape and pastel color.
3. **Behavior**: Focus Mode toggle and dimming opacity, Center popup only, Folder lock switch.
4. **Integration**: Explorer Context Menu, File-on-file drag merge, Right double-click, Space Quick Look.
5. **Updates**: Version checker, release notes, and automated update downloader.

---

## Multi-Language Localization

The application is fully localized into **7 languages**:

| Flag | Language | Native Name | Code |
| :---: | :--- | :--- | :---: |
| 🇦🇲 | **Armenian** | Հայերեն | `hy` |
| 🇬🇧 | **English** | English | `en` |
| 🇷🇺 | **Russian** | Русский | `ru` |
| 🇫🇷 | **French** | Français | `fr` |
| 🇪🇸 | **Spanish** | Español | `es` |
| 🇨🇳 | **Chinese** | 简体中文 | `zh` |
| 🇸🇦 | **Arabic** | العربية | `ar` |

Language can be changed instantly in Settings without restarting the application.

---

## GitHub Automatic Updates

- Integrated directly with GitHub Releases (`hranthakobian/Panak`).
- Periodically checks for new releases in the background without interruptions.
- Displays release notes and allows one-click downloading and upgrading to the latest version.

---

## Installation & Uninstallation

Panak requires **no administrator privileges** to install or run.

### Option 1: One-Click Installer (Recommended)
1. Download `Panak-Installer-vX.X.X.zip` from [Releases](https://github.com/hranthakobian/Panak/releases).
2. Extract the archive and launch **`Setup.exe`** (or `scripts\Install.bat`).
3. Follow the setup wizard; files are installed to `%LocalAppData%\Programs\Panak`.

### Option 2: Command Line (CLI)
```powershell
# Interactive GUI Installer
Panak.exe --install

# Completely Silent Unattended Installation
Panak.exe --install-silent
```

### Uninstallation
Panak can be completely removed using any of the following methods:
- Run **`Uninstall.exe`** or **`Uninstall.bat`**, or
- Open **Panak Settings** and click **Uninstall**, or
- Open **Windows Settings ➔ Apps ➔ Installed apps ➔ Panak ➔ Uninstall**.

The dedicated uninstaller (`Uninstall.exe`) features dynamic Material You branding matching the user's settings, feedback contact links, an option to remove the .NET 8.0 Desktop Runtime, and thoroughly cleans all registry entries, context menus, shortcuts, and application files.

---

## Command Line Interface (CLI)

Panak features a robust IPC architecture supporting the following command-line flags:

```bash
# Open specified folder as a Panak popup
Panak.exe --open "C:\Path\To\Folder"

# Create a Panak folder containing the specified file
Panak.exe --create "C:\Path\To\File.txt"

# Convert an existing folder into a Panak folder (generates 2x2 preview icon)
Panak.exe --convert "C:\Path\To\Folder"

# Create a new empty Panak folder at specified path
Panak.exe --new "C:\Path\To\TargetDirectory"

# Open Settings window
Panak.exe --settings

# Launch background service
Panak.exe --background

# Launch Setup / Installer Wizard
Panak.exe --install

# Run unattended silent installation
Panak.exe --install-silent

# Launch Uninstaller Wizard
Panak.exe --uninstall
```

---

## Project Build & Architecture

### Prerequisites
- **Windows 10 / 11** (x64)
- **.NET 8.0 SDK** (or newer)
- Visual Studio 2022 (with WPF tools) or VS Code

### Building from Source

```powershell
# 1. Clone repository
git clone https://github.com/hranthakobian/Panak.git
cd Panak

# 2. Build main project
dotnet build

# 3. Run unit tests
dotnet test

# 4. Publish Release build
dotnet publish -c Release -r win-x64 --self-contained false -o publish Panak.csproj

# 5. Build standalone single-file Setup.exe installer
.\scripts\BuildSetup.bat

# 6. Build Microsoft Store MSIX package (see STORE_PUBLISHING.md)
.\scripts\BuildMsix.bat
```

### Architecture Tree
```
Panak/
├── App.xaml / App.xaml.cs         # Main application entry point & IPC dispatcher
├── Models/                        # Data models (AppSettings, PanakFolderData, PanakItem)
├── Services/
│   ├── PanakManager.cs            # Folder window manager & transitions
│   ├── IconGenerator.cs           # 2x2 ICO preview generator & cache buster
│   ├── HookService.cs             # Global low-level mouse & keyboard hook manager
│   ├── ExplorerHelper.cs          # UI Automation & Explorer bounding box detector
│   ├── ContextMenuRegistry.cs     # Windows Shell context menu verb registrar
│   ├── LocalizationService.cs     # 7-language localization dictionary engine
│   ├── GitHubUpdateService.cs     # Asynchronous GitHub releases update engine
│   ├── ShellHelper.cs             # Win32 Shell API & desktop wallpaper blurs
│   ├── SingleInstanceIpc.cs       # Named Pipe inter-process communication
│   ├── TrayManager.cs             # Resident notification tray management
│   └── InstallerService.cs        # Program deployment & uninstallation logic
├── Views/
│   ├── FolderPopupWindow.xaml     # Animated Android-style popup card
│   ├── BackdropWindow.xaml        # Focus mode dimming canvas
│   ├── SettingsWindow.xaml        # Modern settings configuration dashboard
│   └── InstallerWindow.xaml       # Animated installer & uninstaller UI
├── Panak.ShellExt/                # COM Shell Drag & Drop drop handler extension
├── Setup/                         # Standalone self-contained single-file installer project
└── Tests/                         # Automated unit & integration tests
```

---

## Support

Contributions, bug reports, and feedback are always welcome:

- **Issues & Suggestions**: [GitHub Issues](https://github.com/hranthakobian/Panak/issues)
- **Discussions**: [GitHub Discussions](https://github.com/hranthakobian/Panak/discussions)
- **Support the Developer**: If you enjoy using Panak, consider buying a coffee at **[Ko-fi](https://ko-fi.com/hranthakobian)**!

---

## License

Distributed under the **MIT** License. See [`LICENSE`](LICENSE) for complete details.

---

<p align="center">
  Crafted with ❤️ to make the Windows desktop more beautiful and productive
</p>
