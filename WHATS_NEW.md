# What's New in Panak v2.0.5

## 🌟 Highlights & Major Features (v2.0.5)

### 1. 📂 Universal Quick Look for Any Windows Folder in File Explorer
* **Quick Look Preview via Space Key**: You can now select or hover over any Windows folder inside Windows File Explorer (or have an active folder open) and press `Space` to instantly preview its contents in Panak's Material You popup card.
* **Toggle Open/Close**: Pressing `Space` or your configured Quick Look hotkey toggles the preview card open and closed smoothly.
* **Explorer Remains Open**: File Explorer windows are preserved when previewing ordinary folders; Explorer is only closed when converting or opening dedicated desktop Panak folders.

### 2. 🖱️ Seamless Right Double-Click in File Explorer
* **Context Menu Dismissal**: Right double-clicking any folder inside Windows File Explorer automatically dismisses the Windows shell context menu (`#32768`) and triggers the instant popup preview.
* **Fast Item Tracking**: Accurately captures the target folder under cursor on first right-click, ensuring instant responsiveness without missed clicks.

### 3. ⚡ Zero-Lag Win32 Text Caret & Focus Detection
* **Thread-Aware GUI Thread Inspection**: Win32 `GetGUIThreadInfo` with the foreground window thread ID detects active text carets (`hwndCaret`) and edit controls without invoking cross-process UI Automation.
* **Seamless Typing & Renaming**: Pressing `Space` while renaming files (F2) or typing in Explorer search/address boxes behaves completely naturally without accidental popup triggers or hook timeouts.

### 4. 🛡️ Clean Context Menu Registration
* **Protected Registry Shell Integration**: Context menu entries ("Բացել Panak-ով" / "Open in Panak") now cleanly resolve to the real `Panak.exe`, safely guarding against test runners and ensuring consistent shell commands.

### 5. 📁 Desktop Folder Integrity & Cut-and-Paste Fix
* **Strict Panak Folder Verification**: Creating and renaming ordinary folders on the Desktop and placing files inside them no longer inadvertently attaches watchers or converts them into Panak-styled folders.
* **Clean Cut & Paste (Move) Operations**: Moving or cutting folders from the Desktop into other folders cleanly unregisters watchers and removes any leftover empty shell artifacts (`desktop.ini` / `panak_icon*.ico`), preventing empty ghost folders from remaining on the Desktop.

---

# What's New in Panak v2.0.4

## 🌟 Highlights & Major Features (v2.0.4)

### 1. 🎯 Windows-Style Desktop Grid Snapping in Previews
* **Snap-to-Grid Movement**: Desktop folder icons in both the Setup & Welcome onboarding wizard (`WelcomeWindow`) and the live Settings preview (`SettingsWindow`) can now be freely dragged and smoothly snap to discrete desktop grid slots (68px horizontal step, 76px vertical step, 12px margin), replicating native Windows desktop behavior.
* **Top-Left Default Positioning**: Preview desktop icons are placed in the top-left corner by default (Settings: `(12, 12)`; Welcome: Folder 1 `(12, 12)`, Folder 2 `(80, 12)`).

### 2. 🧭 Smart 4-Way Popup Collision & Dynamic Anchor Animations
* **4-Way Collision Detection**: Enhanced popup geometry calculation (`CalculateSmartPopupPosition` / `PositionSettingsPreviewPopup`) evaluates available desktop canvas space in all four directions (`spaceRight`, `spaceLeft`, `spaceUp`, `spaceDown`).
* **Adaptive Transform Origin**: Automatically anchors popup card transformations to the optimal corner (Top-Left `(0.0, 0.0)`, Bottom-Left `(0.0, 1.0)`, Top-Right `(1.0, 0.0)`, or Bottom-Right `(1.0, 1.0)`). Scale and fade animations burst organically outward from the folder icon.
* **Sub-Crop Acrylic Wallpaper Tracking**: Dynamic acrylic frosted blur aligns with popup coordinates in real-time as icons move across the desktop.

### 3. 🔍 Desktop Preview Zoom Controls & Scale Badge
* **Header Zoom Controls in Settings**: Added dedicated `−` (zoom out), percentage badge/reset (`100%`), and `+` (zoom in) buttons in the Settings header next to the preview toggle button.
* **Auto-Hiding Controls**: When "Hide Preview" is clicked, the zoom controls group automatically collapses alongside the preview card and restores when toggled back.
* **Ctrl + MouseWheel Zooming**: Holding `Ctrl` while rotating the mouse wheel zooms the preview canvas between `35%` and `150%`, allowing wide multi-column popups (up to 8 columns) to be comfortably inspected.
* **Double/Middle-Click Reset**: Double-clicking or middle-clicking the zoom percentage badge instantly resets scale to 100%.

### 4. 🖱️ Click-Outside to Dismiss Popup
* **Canvas Hit-Testing**: Enabled transparent canvas background hit-testing across all simulated desktop preview surfaces.
* **Outside Click Detection**: Clicking anywhere on the simulated desktop wallpaper outside the active popup and desktop icons instantly closes the folder popup.

### 5. 📜 Welcome Wizard Multi-Folder Scrolling Fix
* **Second Folder Scroll Mode**: Resolved item grid binding and scroll viewer visibility for the second folder preview ("Projects") when switching between Slider and Scrollbar paging modes in `WelcomeWindow`.

---

# What's New in Panak v2.0.3

## 🌟 Highlights & Major Features (v2.0.3)

### 1. ⚡ Extreme Performance & Low-End Windows 10 Optimization
* **Zero-Latency Mouse & Keyboard Hooks**: Fixed phantom drag-state tracking (`_isLeftButtonDown`) and added fast Win32 class filtering (`IsExplorerOrDesktopWindow`) so UI Automation (`FromPoint`) is never invoked over browsers, games, or IDEs.
* **Async Hook Dispatching**: Replaced synchronous `Dispatcher.Invoke` in low-level hooks with `Dispatcher.BeginInvoke` to eliminate cursor stutter.
* **4x Downsampled Acrylic Blur & Direct LockBits**: Screen capture for blur underlays now downsamples by 4x (16x fewer pixels) and copies directly via `Bitmap.LockBits` without GDI handle leaks.
* **Zero Idle GPU & Timer Load**: Disabled continuous 66ms/100ms screen capture timers when no live wallpaper engine is active; capped `WavyProgressBar` and marquee animations to 30–33 FPS and paused when hidden.
* **Instant Asynchronous Folder Opening**: Popups open immediately with cached/extension icons (`SHGFI_USEFILEATTRIBUTES`) while high-res image and executable thumbnails decode asynchronously in the background.
* **Flicker-Free Desktop Refresh**: Replaced disruptive `F5` keystroke and Explorer COM refresh loops with lightweight debounced `InvalidateRect`.

---

# What's New in Panak v2.0.2

## 🌟 Highlights & Major Features (v2.0.2)

### 1. 🔢 Arbitrary PIN Length & Fluid Layout
* Removed the 4-digit requirement: PIN codes can now have any length.
* Added explicit on-screen Backspace (`⌫`), `0`, and Submit (`➔`) buttons alongside auto-unlock on exact match.
* Integrated fluid horizontal push animation (`PinDotsTranslate.X`) that smoothly shifts previous dots aside when typing or deleting digits.

### 2. ⌨️ Full Keyboard PIN Input
* Full keyboard typing support: digits can now be typed from both top-row number keys (`0`-`9`) and the Numpad (`NumPad0`-`NumPad9`).
* `HandleTextInput` support for all system keyboard layouts (Armenian, Russian, English, etc.) and accessibility input tools.
* Keyboard hotkeys: `Backspace` (delete digit & pull back), `Enter` (submit & verify PIN), `Escape` (clear PIN / dismiss), and `Delete` (clear).
* Tactile visual feedback: typing physical keys triggers smooth on-screen key bounce animations (`ScaleTransform` `0.90` -> `1.0`).
* Automatic focus management (`FocusInput()`) ensuring the PIN keypad is always ready for typing without mouse clicks.

### 3. ✨ Expressive Material 3 Dot Settling & Bug Fixes
* Pre-compiled and frozen vector geometries for Circle, Squircle, Star, Scallop, and Arch (`ExpressiveGeometries`).
* Fixed initial dot flash glitch: shapes now spawn directly at full scale (`1.15` -> `1.0`) and smoothly morph down to 11px circles after ~260ms.
* Resolved `FormatException` token issue in Scallop bezier geometry definition.

---

# What's New in Panak v2.0.1

## 🌟 Highlights & Major Features (v2.0.1)

### 1. ✏️ Folder Edit & Context Menu Parity
* Updated folder renaming action to «Խմբագրել» (Edit) across tooltips, labels, and context menus.
* Replaced context menus with borderless, floating acrylic dropdown panels with lighter frosted glass backgrounds.

### 2. 🎨 Adaptive Theme Mode & Icon Coloring
* Live desktop icon shape tone now dynamically adapts to theme mode (darker tone in dark mode, light pastel in light mode).

### 3. 🎬 Motion Graphics & Vector Animations
* Settings paging mode illustrations now faithfully reproduce the exact vector keyframes of `Silder.svg` and `Scroll.svg` with dynamic M3 colors.
* Added 64-point vector shape morphing between Circle, Squircle, 4-pointed Star, Scallop, and Arch in the desktop preview.
* Integrated sliding animated indicator pills for theme mode and shape switchers.

### 4. ⭐ 4-Pointed Star Shape
* Replaced the third icon shape with the exact 4-pointed star geometry from `Star 2.svg` across XAML UI, GDI+ icon generators, and localization («Քառաթև աստղ»).

### 5. 🪟 Real-time Acrylic Parity
* Continuous real-time desktop background blur tracking in Settings preview.
* Unified WelcomeWindow Step 1 preview card to exact acrylic frosted glass architecture.

---

## 🌟 Highlights & Major Features (v2.0.0)

### 1. 🔒 Comprehensive Multi-Factor Folder Security & Locking
* **Individual Folder Locks**: Lock folders independently with Password (PBKDF2/SHA-256), PIN (4-8 digits), or phone-like drag-to-draw 3x3 Pattern.
* **Settings Lock**: Lock the entire Settings interface with Password, PIN, or Pattern.
* **Explorer Protection**: The "Open in Windows Explorer" button and context menu item are completely hidden and blocked when a folder is locked.
* **Tamper & Offline Resistance**: Folder locks write persistent security markers to `panak_lock.json` and `desktop.ini` attributes (`PanakLocked=1`).
* **Uninstaller Safeguard**: The Standalone Uninstaller strictly blocks uninstallation if any folder in the system is still locked, preventing accidental loss of access.

---

### 2. 📦 Unpack Panak Folder («Ապահավաքել պանակը»)
* **One-Click Unpack**: Move all contents out of the Panak container directly into its parent folder, clean up the container, and dissolve the custom folder state. Accessible from both header actions and folder context menu.

---

### 3. 🖥️ Redesigned Two-Pane Settings Window with Live Desktop Preview
* **Dual-Column Layout**: Left column houses scrollable setting cards; right column displays a full-height Live Preview with real desktop wallpaper, folder icon, and popup card.
* **Four Section Tabs with Smooth Scroll**: Instant navigation between «Արտաքին», «Դիրք», «Անվտանգություն», and «Համակարգ» with fluid cubic-ease scrolling.
* **Collapsible Preview**: Header button collapses or expands the live preview pane, smoothly transitioning window width between 880px and 520px.
* **Color First**: Color & Hue card is positioned before the Theme Mode card for improved setup ergonomics.

---

### 4. 🪟 Windows 10 Black Blur Fix & Visual Polish
* **BitBlt Exclude Window Compatibility**: Exclude-from-capture is applied strictly on Windows 11 (Build >= 22000), eliminating black rectangles on Windows 10 and falling back to clean wallpaper sampling.
* **Authentic Uninstaller Assets**: Modern dynamic vector SVG trash can with theme-bound brushes and authentic uninstaller icons.

---

# What's New in Panak v1.9.0

## 🌟 Highlights & Major Features (v1.9.0)

### 1. 🖼️ Real-Time Wallpaper Engine & Desktop Background in Preview
* **Live Wallpaper Capture**: The Setup Wizard and Welcome experience capture the live Windows desktop background in real time. If **Wallpaper Engine** or interactive live wallpapers are active, Panak captures frames directly from `WorkerW` to display live, moving wallpaper inside the preview cards.
* **Direct Blur Slider**: An interactive blur slider is integrated directly into the live preview toolbar, offering instantaneous feedback on the wallpaper underlay.
* **Fallback High-Res Wallpaper**: When static wallpapers are active, Panak reads the exact full-resolution Windows wallpaper (`TranscodedWallpaper` / registry).

---

### 2. 🌫️ True Folder Background Blur & Material 3 Rounded Corners
* **Rounded Corners (28px)**: Standardized `CornerRadius="28"` across all folder popup cards, frosted glass underlays, and preview surfaces for authentic Material 3 Expressive aesthetics.
* **Real Wallpaper Blur Effect**: Folder blur is applied directly to the desktop backdrop behind the popup using hardware-accelerated shaders and customizable `FolderBlurRadius` (2px to 60px).
* **Focus Mode Unpin Fix**: Resolved the z-order drop/flicker where unpinned popups could briefly fall behind the dimmed backdrop window.

---

### 3. 📂 Authentic Desktop Folder Icon Preview & Origin Animation
* **Authentic 2x2 Desktop Icon Plate**: Preview renders an authentic folder icon featuring the selected Expressive shape plate (Circle, Squircle, Clover, Scallop, Arch), a 2x2 miniature icon grid (`media_1791914326888.png`), and a shadowed label.
* **Origin-Based Opening Motion**: When `OpenInCenterOnly` is disabled, the folder expands and morphs directly from the desktop icon; when enabled, it scales smoothly from the screen center.
* **Interactive Open/Close Toggle**: Clicking the desktop icon or preview close button (`✕`) triggers realistic cubic-ease scale and fade animations.

---

### 4. 🗂️ Authentic Folder Header & Dynamic Dynamic Sizing
* **Authentic Header**: Features the folder title, an items count badge (e.g., "18 նիշք" / "18 items"), and four interactive action buttons (Pin, Edit, Explorer, Close) matching `media_1791914326899.png`.
* **Dynamic Sizing**: Rows and columns represent maximum boundaries. When a folder has fewer items, the popup dimensions conform tightly to the exact count without vacant space.
* **Dynamic Slider Pages**: Pages are calculated dynamically without empty trailing slides. Pager controls and dots are automatically hidden when there is only a single page. Active dots feature an elongated pill indicator (18x6), and any dot can be clicked to jump between pages.

---

### 5. 🎛️ Smooth Animated Toggles across All Windows
* **Fluid Storyboard Animations**: Standardized Material 3 Expressive switch toggles across `WelcomeWindow`, `SettingsWindow`, `SetupWindow`, and `UninstallerWindow` with smooth sliding animations (`Canvas.Left` 2 to 21 with `CubicEase` over 0.22s).

---

### 6. 🎲 Random Folder Attributes & Custom Color Swatches
* **Random Attributes on Folder Creation**: Added toggles for Random Shape, Random Color, and Random Opacity in both Settings and Welcome Wizard, dynamically applied when new Panak folders are created.
* **Custom Hue Swatches**: Clicking the Hue color indicator circle immediately adds the current tint to custom palette colors (`CustomPaletteColors`), persisting it and rendering accessible swatches in Settings.
* **JSON Settings Export & Import**: Convenient Export and Import buttons in the Settings footer allow backing up and restoring complete configurations via `.json`.

---

### 7. 🌐 Comprehensive Multi-Language Localization
* **Cleaned Parenthetical English**: Removed extraneous parenthetical English labels across all 7 supported languages (`hy`, `en`, `ru`, `fr`, `es`, `zh`, `ar`).
* **Fully Localized Controls**: Complete localization for all new keys, including "Step", "Live Preview", "Random", "Export/Import Settings", and count badges.

---

## 📦 Distribution & Packaging
* **Version**: `v1.9.0`
* **Standalone Installer**: `publish\Setup.exe` (Single-file self-contained installer with embedded payload).
* **Installer Package**: `Panak-Installer-v1.9.0.zip` ready for distribution.
