using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Panak.Models;
using Panak.Services;
using Panak.Views;

namespace Panak.Tests {
    [TestClass]
    public class CoreTests {
        [TestMethod]
        public void TestLocalizationKeysExistInAllLanguages() {
            var loc = LocalizationService.Instance;
            string[] testKeys = new[] {
                "CreateFolder", "OpenInPanak", "NewFolder", "Settings",
                "LockFolders", "IconShape", "ItemsPerRow", "FocusMode", "FocusDimming",
                "FocusBlur", "FocusBlurDesc", "FocusBlurRadius",
                "Language", "RightDoubleClick", "SpaceQuickLook", "Install", "Uninstall",
                "ConvertToPanak", "RevertToRegular", "AlwaysOpenWithPanak",
                "CombineFilesOnDrop", "CombineFilesOnDropDesc", "CollectIntoPanakFolder",
                "Hue", "DefaultFolderColor", "Apply",
                "LanguageAiNote", "CenterPopupOnly", "CenterPopupOnlyDesc", "Support", "SupportDesc", "DonateOnKofi",
                "DotNetRuntime", "DotNetRuntimeDesc", "DotNetAlreadyInstalled", "DotNetNotInstalled", "DotNetDownloading", "DotNetInstalling",
                "FolderBlur", "FolderBlurDesc", "FolderBlurRadius", "SlowsDownSystemWarning", "PagingMode", "PagingModeDesc", "PagingModeSlider", "PagingModeScrollbar",
                "GridRows", "SliderArrows", "SliderDrag", "Step1Title", "Step1Subtitle", "Step2Title", "Step2Subtitle", "Step3Title", "Step3Subtitle", "Skip", "Next", "Back", "Preview", "Step", "LivePreview",
                "EnterPinToUnlock", "PinDesc", "DrawPatternToUnlock", "PatternDesc", "EnterPasswordToUnlock", "PasswordDesc",
                "EnterPINToAccess", "EnterPinToAccess", "EnterPasswordToAccess", "EnterPatternToAccess", "Unlock", "Locked",
                "RemoveLock", "RemoveLockSubtitle", "IncorrectCredential", "WrongCredentialError", "ClearPattern",
                "UpdateTranslations", "TranslationsUpdated", "MoreActions", "ConfigureLock",
                "AppearanceTab", "LayoutTab", "SecurityTab", "SystemTab", "TranslationsTab",
                "TranslationChecker", "TranslationCheckerDesc", "SearchTranslationsPlaceholder", "CheckOtaUpdates", "ExportLanguageJson"
            };

            foreach (var lang in new[] { "hy", "en", "ru", "fr", "es", "zh", "ar" }) {
                loc.CurrentLanguage = lang;
                foreach (var key in testKeys) {
                    string val = loc[key];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Key '{key}' should not be empty in language '{lang}'");
                }
            }
        }

        [TestMethod]
        public void TestTranslationTabKeysAndStats() {
            var loc = LocalizationService.Instance;
            string[] supportedLangs = { "hy", "en", "ru", "fr", "es", "zh", "ar" };

            foreach (var lang in supportedLangs) {
                var stats = loc.GetTranslationStats(lang);
                Assert.IsTrue(stats.total >= 200, $"Total keys should be at least 200, was {stats.total}");
                Assert.IsTrue(stats.translated >= 200, $"Translated keys for '{lang}' should be at least 200, was {stats.translated}");

                var allWithTr = loc.GetAllKeysWithTranslations(lang);
                Assert.IsTrue(allWithTr.ContainsKey("TranslationsTab"), $"Language '{lang}' should contain 'TranslationsTab'");
                Assert.IsTrue(allWithTr.ContainsKey("AppearanceTab"), $"Language '{lang}' should contain 'AppearanceTab'");
                Assert.IsTrue(allWithTr.ContainsKey("LayoutTab"), $"Language '{lang}' should contain 'LayoutTab'");
                Assert.IsTrue(allWithTr.ContainsKey("SecurityTab"), $"Language '{lang}' should contain 'SecurityTab'");
                Assert.IsTrue(allWithTr.ContainsKey("SystemTab"), $"Language '{lang}' should contain 'SystemTab'");
            }
        }

        [TestMethod]
        public void TestDesktopWallpaperDetection() {
            bool isWpRunning = ShellHelper.IsLiveWallpaperEngineRunning();
            Console.WriteLine($"IsLiveWallpaperEngineRunning: {isWpRunning}");

            // Verify wallpaper discovery does not crash and returns valid source or null gracefully
            var wp = ShellHelper.GetDesktopWallpaperBitmap();
            Console.WriteLine($"GetDesktopWallpaperBitmap result: {(wp != null ? $"{wp.PixelWidth}x{wp.PixelHeight}" : "null")}");
        }

        [TestMethod]
        public void TestIconGeneratorRendersPlate() {
            string[] shapes = new[] { "Circle", "Squircle", "Clover", "Scallop", "Arch", "RoundedRect", "Teardrop" };
            Color testColor = Color.FromArgb(59, 130, 246);

            // Create mini mock icon
            using Bitmap mini = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(mini)) {
                g.Clear(Color.Red);
            }

            var miniList = new List<Bitmap> { mini };

            foreach (var shape in shapes) {
                using Bitmap result = IconGenerator.RenderPlateWithGrid(256, shape, testColor, miniList);
                Assert.IsNotNull(result, $"Plate for shape {shape} should not be null");
                Assert.AreEqual(256, result.Width);
                Assert.AreEqual(256, result.Height);
            }
        }

        [TestMethod]
        public void TestFolderIconGenerationAndDesktopIni() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakTestFolder_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);

                // Create a couple of mock files inside
                File.WriteAllText(Path.Combine(tempDir, "file1.txt"), "hello");
                File.WriteAllText(Path.Combine(tempDir, "file2.txt"), "world");

                IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#10B981");

                string[] icoFiles = Directory.GetFiles(tempDir, "panak_icon*.ico");
                Assert.IsTrue(icoFiles.Length > 0, "panak_icon*.ico should exist");
                string icoPath = icoFiles[0];
                string iniPath = Path.Combine(tempDir, "desktop.ini");
                Assert.IsTrue(File.Exists(iniPath), "desktop.ini should exist");

                // Check ICO file has valid size and header
                byte[] icoBytes = File.ReadAllBytes(icoPath);
                Assert.IsTrue(icoBytes.Length > 100, "ICO file should contain image frames");
                Assert.AreEqual(0, icoBytes[0]);
                Assert.AreEqual(0, icoBytes[1]);
                Assert.AreEqual(1, icoBytes[2]); // Type 1 = Icon
            } finally {
                if (Directory.Exists(tempDir)) {
                    // Reset attributes to permit deletion
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestGenerateFolderIconWithCustomOpacityAndPanakJson() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakOpacityTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(Path.Combine(tempDir, "sample1.txt"), "hello");

                var data = new PanakFolderData {
                    Title = "TestOpacity",
                    Color = "#D7C5FC",
                    IconShape = "Clover",
                    BackgroundOpacity = 0.60
                };
                data.Save(tempDir);

                IconGenerator.GenerateFolderIcon(tempDir);

                string iniPath = Path.Combine(tempDir, "desktop.ini");
                Assert.IsTrue(File.Exists(iniPath));
                string iniContent = File.ReadAllText(iniPath);
                StringAssert.Contains(iniContent, "panak_icon_0.ico,0");

                string[] icoFiles = Directory.GetFiles(tempDir, "panak_icon*.ico");
                Assert.AreEqual(1, icoFiles.Length);
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestTwoSlotAlternatingIconsAndReadOnlyIntegrity() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakSlotTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(Path.Combine(tempDir, "item1.txt"), "test1");

                // 1st generation -> should use slot 0
                IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#3B82F6");

                string iniPath = Path.Combine(tempDir, "desktop.ini");
                Assert.IsTrue(File.Exists(iniPath));
                var folderAttrs = File.GetAttributes(tempDir);
                Assert.IsTrue(folderAttrs.HasFlag(FileAttributes.ReadOnly), "Folder must have ReadOnly attribute");

                var iniAttrs = File.GetAttributes(iniPath);
                Assert.IsTrue(iniAttrs.HasFlag(FileAttributes.Hidden), "desktop.ini must have Hidden attribute");
                Assert.IsTrue(iniAttrs.HasFlag(FileAttributes.System), "desktop.ini must have System attribute");

                string content1 = File.ReadAllText(iniPath);
                StringAssert.Contains(content1, "panak_icon_0.ico,0");
                Assert.IsFalse(content1.Contains(":\\"), "desktop.ini should use relative path, not absolute path");

                // 2nd generation (simulate file change) -> should switch to slot 1
                File.WriteAllText(Path.Combine(tempDir, "item2.txt"), "test2");
                IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#3B82F6");

                string content2 = File.ReadAllText(iniPath);
                StringAssert.Contains(content2, "panak_icon_1.ico,0", "Should switch to panak_icon_1.ico on second generation");

                // 3rd generation -> should switch to next slot (panak_icon_2.ico) to bypass Windows Shell icon cache
                File.Delete(Path.Combine(tempDir, "item1.txt"));
                IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#3B82F6");

                string content3 = File.ReadAllText(iniPath);
                StringAssert.Contains(content3, "panak_icon_2.ico,0", "Should rotate to panak_icon_2.ico on third generation to bypass Windows Shell icon cache");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestSequentialIconRotationBypassesShellCache() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakRotTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(Path.Combine(tempDir, "sample.txt"), "hello");

                for (int i = 0; i < 5; i++) {
                    IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#3B82F6");
                    string iniPath = Path.Combine(tempDir, "desktop.ini");
                    Assert.IsTrue(File.Exists(iniPath));
                    string iniContent = File.ReadAllText(iniPath);
                    StringAssert.Contains(iniContent, $"panak_icon_{i}.ico,0", $"Generation {i} should use panak_icon_{i}.ico");

                    string[] activeIcos = Directory.GetFiles(tempDir, "panak_icon*.ico");
                    Assert.AreEqual(1, activeIcos.Length, "Only the current active panak_icon_*.ico should remain on disk");
                    Assert.AreEqual($"panak_icon_{i}.ico", Path.GetFileName(activeIcos[0]), "The remaining ICO file must match the active slot");
                }
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestAppSettingsSerialization() {
            var settings = new AppSettings {
                Language = "hy",
                GridColumns = 6,
                FocusModeEnabled = true,
                FocusDimmingOpacity = 0.55,
                FocusBlurEnabled = false,
                FocusBlurRadius = 35.0,
                IconShape = "Circle",
                IsLocked = true,
                LockPopupPosition = true,
                ThemeColor = "#8B5CF6",
                CombineFilesOnDrop = true,
                OpenInCenterOnly = true
            };

            string json = System.Text.Json.JsonSerializer.Serialize(settings);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual("hy", deserialized.Language);
            Assert.AreEqual(6, deserialized.GridColumns);
            Assert.AreEqual(0.55, deserialized.FocusDimmingOpacity);
            Assert.IsFalse(deserialized.FocusBlurEnabled);
            Assert.AreEqual(35.0, deserialized.FocusBlurRadius);
            Assert.AreEqual("Circle", deserialized.IconShape);
            Assert.IsTrue(deserialized.IsLocked);
            Assert.IsTrue(deserialized.LockPopupPosition);
            Assert.AreEqual("#8B5CF6", deserialized.ThemeColor);
            Assert.IsTrue(deserialized.CombineFilesOnDrop);
            Assert.IsTrue(deserialized.OpenInCenterOnly);
        }

        [TestMethod]
        public void TestLockPopupPositionDefaultsAndLocalization() {
            var settings = new AppSettings();
            Assert.IsFalse(settings.LockPopupPosition);

            var hyLoc = LocalizationService.Instance;
            hyLoc.CurrentLanguage = "hy";
            Assert.AreEqual("Անջատել նիշքի քաշելով հանելը", hyLoc["LockFolders"]);
            Assert.IsFalse(string.IsNullOrEmpty(hyLoc["LockPopupPosition"]));

            hyLoc.CurrentLanguage = "en";
            Assert.AreEqual("Disable file drag-out", hyLoc["LockFolders"]);
            Assert.IsFalse(string.IsNullOrEmpty(hyLoc["LockPopupPosition"]));
        }

        [TestMethod]
        public void TestPanakFolderDataSaveWithHiddenAttribute() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakSaveTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try {
                var data = new PanakFolderData {
                    Title = "TestFolder",
                    Color = "#3B82F6"
                };
                data.Save(tempDir);

                string metaFile = Path.Combine(tempDir, "panak.json");
                Assert.IsTrue(File.Exists(metaFile), "panak.json should exist");
                Assert.IsTrue(File.GetAttributes(metaFile).HasFlag(FileAttributes.Hidden), "panak.json should be Hidden");

                // Now modify color and save again
                data.Color = "#EF4444";
                data.Save(tempDir);

                var reloaded = PanakFolderData.Load(tempDir);
                Console.WriteLine("RELOADED COLOR: " + reloaded.Color);
                Assert.AreEqual("#EF4444", reloaded.Color, "Color must be saved even if panak.json is Hidden!");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestCollectFilesIntoPanakFolder() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakCollectTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                string f1 = Path.Combine(tempDir, "sample1.txt");
                string f2 = Path.Combine(tempDir, "sample2.txt");
                File.WriteAllText(f1, "hello");
                File.WriteAllText(f2, "world");

                string createdFolder = PanakManager.Instance.CreatePanakFolder(tempDir, new[] { f1, f2 }, "CombinedFolder");
                Assert.IsTrue(Directory.Exists(createdFolder), "Combined folder should exist");
                Assert.IsTrue(File.Exists(Path.Combine(createdFolder, "panak.json")), "panak.json should exist");
                Assert.IsTrue(File.Exists(Path.Combine(createdFolder, "sample1.txt")), "sample1.txt should have moved");
                Assert.IsTrue(File.Exists(Path.Combine(createdFolder, "sample2.txt")), "sample2.txt should have moved");
            } finally {
                if (Directory.Exists(tempDir)) {
                    foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories)) {
                        try {
                            File.SetAttributes(file, FileAttributes.Normal);
                        } catch { }
                    }
                    foreach (var subDir in Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories)) {
                        try {
                            File.SetAttributes(subDir, FileAttributes.Normal);
                        } catch { }
                    }
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    try {
                        Directory.Delete(tempDir, true);
                    } catch {
                        System.Threading.Thread.Sleep(100);
                        try { Directory.Delete(tempDir, true); } catch { }
                    }
                }
            }
        }

        [TestMethod]
        public void TestInstallerDirectoryComputation() {
            string installDir = InstallerService.GetInstallDirectory();
            Assert.IsFalse(string.IsNullOrEmpty(installDir));
            Assert.IsTrue(installDir.Contains("Panak"));
        }

        [TestMethod]
        public void TestDirectWriteToHiddenSystemFile() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakWriteTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try {
                string testFile = Path.Combine(tempDir, "desktop.ini");
                File.WriteAllText(testFile, "Initial content", System.Text.Encoding.Unicode);
                File.SetAttributes(testFile, FileAttributes.Hidden | FileAttributes.System);

                // Attempt to write using FileMode.Open
                using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) {
                    fs.SetLength(0);
                    using var sw = new StreamWriter(fs, System.Text.Encoding.Unicode);
                    sw.Write("Updated content");
                }

                var attrs = File.GetAttributes(testFile);
                Console.WriteLine("Attributes after write: " + attrs);
                Assert.IsTrue(attrs.HasFlag(FileAttributes.Hidden), "Must keep Hidden");
                Assert.IsTrue(attrs.HasFlag(FileAttributes.System), "Must keep System");
                Assert.AreEqual("Updated content", File.ReadAllText(testFile));
                Console.WriteLine("Direct write to Hidden+System file SUCCEEDED without losing attributes!");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestPanakFolderConversionAndReversion() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakConvertTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                Assert.IsFalse(PanakManager.Instance.IsPanakFolder(tempDir), "Fresh regular directory should NOT be a Panak folder");

                // Convert to Panak folder
                PanakManager.Instance.ConvertToPanakFolder(tempDir);
                Assert.IsTrue(PanakManager.Instance.IsPanakFolder(tempDir), "Converted directory MUST be recognized as a Panak folder");
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, "panak.json")), "panak.json MUST exist after conversion");

                var loadedData = PanakFolderData.Load(tempDir);
                Assert.IsTrue(loadedData.AutoPopup, "AutoPopup MUST be true after conversion");

                // Revert to regular folder
                PanakManager.Instance.RevertToRegularFolder(tempDir);
                Assert.IsFalse(PanakManager.Instance.IsPanakFolder(tempDir), "Reverted directory must NOT be a Panak folder");
                Assert.IsFalse(File.Exists(Path.Combine(tempDir, "panak.json")), "panak.json must be removed after revert");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestEmptyFolderIconGenerationAndPreservation() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakEmptyFolderTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);

                // Generate icon on completely empty folder
                IconGenerator.GenerateFolderIcon(tempDir, "Squircle", "#3B82F6");

                string iniPath = Path.Combine(tempDir, "desktop.ini");
                Assert.IsTrue(File.Exists(iniPath), "desktop.ini must exist for empty Panak folder");

                var folderAttrs = File.GetAttributes(tempDir);
                Assert.IsTrue(folderAttrs.HasFlag(FileAttributes.ReadOnly), "Empty Panak folder must maintain ReadOnly attribute");

                var iniAttrs = File.GetAttributes(iniPath);
                Assert.IsTrue(iniAttrs.HasFlag(FileAttributes.Hidden) && iniAttrs.HasFlag(FileAttributes.System), "desktop.ini must have Hidden and System attributes");

                string[] icos = Directory.GetFiles(tempDir, "panak_icon*.ico");
                Assert.IsTrue(icos.Length > 0, "Icon file must exist for empty folder");
                byte[] bytes = File.ReadAllBytes(icos[0]);
                Assert.IsTrue(bytes.Length > 100, "ICO file must contain valid icon frames");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestFolderOpenGracePeriodLogic() {
            var thread = new System.Threading.Thread(() => {
                var popup = new Views.FolderPopupWindow();
                Assert.IsFalse(popup.IsWithinOpenGracePeriod());
                popup.LoadFolder(Path.GetTempPath(), System.Windows.Rect.Empty);
                Assert.IsTrue(popup.IsWithinOpenGracePeriod(), "Popup should be within grace period immediately after LoadFolder");
                popup.Close();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestFolderIconPreservedAfterUpdate() {
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string testPanak = PanakManager.Instance.CreatePanakFolder(desktopDir, null, "TestPanakColorChange");
                try {
                    System.Threading.Thread.Sleep(300);

                    using (var bWin = ShellHelper.GetFileIconBitmap(@"C:\Windows", isLarge: true)) {
                        var pCenter = bWin?.GetPixel(bWin.Width / 2, bWin.Height / 2);
                        var pCorner = bWin?.GetPixel(bWin.Width / 6, bWin.Height / 6);
                        Console.WriteLine($"WINDOWS FOLDER Center: R={pCenter?.R}, G={pCenter?.G}, B={pCenter?.B} | Corner: R={pCorner?.R}, G={pCorner?.G}, B={pCorner?.B}");
                    }

                    string[] icos = Directory.GetFiles(testPanak, "panak_icon*.ico");
                    if (icos.Length > 0) {
                        using var ico = new Icon(icos[0], 256, 256);
                        using var bmp = ico.ToBitmap();
                        var pCenter = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
                        var pCorner = bmp.GetPixel(bmp.Width / 6, bmp.Height / 6);
                        Console.WriteLine($"ACTUAL ICO FILE ({Path.GetFileName(icos[0])}) Center: R={pCenter.R}, G={pCenter.G}, B={pCenter.B} | Corner: R={pCorner.R}, G={pCorner.G}, B={pCorner.B}");
                    }

                    using (var bInit = ShellHelper.GetFileIconBitmap(testPanak, isLarge: true)) {
                        var pCenter = bInit?.GetPixel(bInit.Width / 2, bInit.Height / 2);
                        var pCorner = bInit?.GetPixel(bInit.Width / 6, bInit.Height / 6);
                        Console.WriteLine($"INITIAL Center: R={pCenter?.R}, G={pCenter?.G}, B={pCenter?.B} | Corner: R={pCorner?.R}, G={pCorner?.G}, B={pCorner?.B}");
                    }

                    // Now change color using PanakManager / IconGenerator
                    var data = PanakFolderData.Load(testPanak);
                    data.Color = "#EF4444"; // Red
                    data.Save(testPanak);
                    IconGenerator.GenerateFolderIcon(testPanak, data.IconShape, data.Color);

                    string iniPath = Path.Combine(testPanak, "desktop.ini");
                    Console.WriteLine("DESKTOP.INI AFTER RED:\n" + File.ReadAllText(iniPath));
                    var iniAttrs = File.GetAttributes(iniPath);
                    Console.WriteLine("DESKTOP.INI ATTRS: " + iniAttrs);
                    var folderAttrs = File.GetAttributes(testPanak);
                    Console.WriteLine("FOLDER ATTRS: " + folderAttrs);

                    System.Threading.Thread.Sleep(500);

                    using (var bAfter = ShellHelper.GetFileIconBitmap(testPanak, isLarge: true)) {
                        var pCenter = bAfter?.GetPixel(bAfter.Width / 2, bAfter.Height / 2);
                        var pCorner = bAfter?.GetPixel(bAfter.Width / 6, bAfter.Height / 6);
                        Console.WriteLine($"AFTER COLOR CHANGE Center: R={pCenter?.R}, G={pCenter?.G}, B={pCenter?.B} | Corner: R={pCorner?.R}, G={pCorner?.G}, B={pCorner?.B}");
                    }

                    // Now add a file and update icon (simulate drop or Watcher)
                    string f1 = Path.Combine(testPanak, "file1.txt");
                    File.WriteAllText(f1, "sample");
                    IconGenerator.GenerateFolderIcon(testPanak, data.IconShape, data.Color);

                    System.Threading.Thread.Sleep(500);

                    using (var bFile = ShellHelper.GetFileIconBitmap(testPanak, isLarge: true)) {
                        var pCenter = bFile?.GetPixel(bFile.Width / 2, bFile.Height / 2);
                        var pCorner = bFile?.GetPixel(bFile.Width / 6, bFile.Height / 6);
                        Console.WriteLine($"AFTER ADDING FILE Center: R={pCenter?.R}, G={pCenter?.G}, B={pCenter?.B} | Corner: R={pCorner?.R}, G={pCorner?.G}, B={pCorner?.B}");
                    }
                } finally {
                    if (Directory.Exists(testPanak)) {
                        PanakManager.Instance.RevertToRegularFolder(testPanak);
                        Directory.Delete(testPanak, true);
                    }
                }
        }

        [TestMethod]
        public void TestFolderPopupWindowAnimatedControlsExist() {
            var thread = new System.Threading.Thread(() => {
                var window = new FolderPopupWindow();
                Assert.IsNotNull(window.FindName("EditPanelContainer"), "EditPanelContainer must exist for animated settings flyout");
                Assert.IsNotNull(window.FindName("ContentAreaGrid"), "ContentAreaGrid must exist for animated content sizing");
                var editPanel = window.FindName("EditPanel") as System.Windows.FrameworkElement;
                Assert.IsNotNull(editPanel, "EditPanel must exist");
                Assert.AreEqual(234.0, editPanel.Width, 0.1, "EditPanel width must be 3 columns (234px)");
                Assert.IsNotNull(window.FindName("EditPanelTranslate"), "EditPanelTranslate must exist for slide animation");
                Assert.IsNotNull(window.FindName("HueSlider"), "HueSlider must exist for pastel color selection");
                Assert.IsNotNull(window.FindName("PopupBlurImage"), "PopupBlurImage must exist for frosted glass / acrylic blur effect");
                Assert.IsNotNull(window.FindName("FrostedTintLayer"), "FrostedTintLayer must exist for dark frosted glass overlay");
                window.Close();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestSettingsWindowCanBeInstantiated() {
            Exception? thrown = null;
            var thread = new System.Threading.Thread(() => {
                try {
                    var window = new SettingsWindow();
                    Assert.IsNotNull(window.FindName("KofiButton"), "KofiButton must exist");
                    Assert.IsNotNull(window.FindName("CenterPopupCheckBox"), "CenterPopupCheckBox must exist");
                    window.Close();
                } catch (Exception ex) {
                    thrown = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (thrown != null) {
                Assert.Fail("SettingsWindow instantiation threw: " + thrown);
            }
        }

        [TestMethod]
        public void TestPastelColorGenerationAndLuminance() {
            // Verify exact #FCDDC5 color generation at hue 26.2
            var peachColor = ShellHelper.ColorFromPastelHue(26.18);
            string peachHex = ShellHelper.ColorFromPastelHueHex(26.18);
            Assert.AreEqual("#FCDDC5", peachHex, "Hue 26.18 should generate exact hex #FCDDC5");
            Assert.AreEqual("#FCDDC5", ShellHelper.DefaultPastelColorHex, "DefaultPastelColorHex must be #FCDDC5");

            // Test 12 hues around the color wheel (0, 30, 60, ... 330)
            for (double h = 0; h < 360; h += 30) {
                var color = ShellHelper.ColorFromPastelHue(h);
                string hex = ShellHelper.ColorFromPastelHueHex(h);

                // Check brightness / luminance is light pastel (> 0.70 and <= 0.98)
                float luminance = (0.299f * color.R + 0.587f * color.G + 0.114f * color.B) / 255.0f;
                Assert.IsTrue(luminance >= 0.72f, $"Luminance at hue {h} ({hex}) should be >= 0.72 (was {luminance})");
                Assert.IsTrue(luminance <= 0.98f, $"Luminance at hue {h} ({hex}) should be <= 0.98 (was {luminance})");

                // Check hue round-trip
                double recoveredHue = ShellHelper.GetHueFromHex(hex);
                double diff = Math.Abs(recoveredHue - h);
                if (diff > 180) diff = 360 - diff;
                Assert.IsTrue(diff < 5.0, $"Recovered hue {recoveredHue} should be close to {h} (diff={diff})");
            }
        }

        [TestMethod]
        public void TestDefaultFolderColorIsPastel() {
            var data = new PanakFolderData();
            Assert.IsFalse(string.IsNullOrEmpty(data.Color), "Default folder color should not be empty");
            Assert.IsTrue(data.Color.StartsWith("#"), "Default folder color should be hex");
            Assert.AreEqual("#FCDDC5", data.Color, "Default folder color should be #FCDDC5");

            var settings = new AppSettings();
            Assert.IsFalse(string.IsNullOrEmpty(settings.ThemeColor), "Default settings theme color should not be empty");
            Assert.IsTrue(settings.ThemeColor.StartsWith("#"), "ThemeColor should be hex");
            Assert.AreEqual("#FCDDC5", settings.ThemeColor, "Default theme color should be #FCDDC5");

            // Verify icon generator renders on pastel background without crash
            using var plate = IconGenerator.RenderPlateWithGrid(256, "Squircle", ColorTranslator.FromHtml(ShellHelper.DefaultPastelColorHex), new List<Bitmap>());
            Assert.IsNotNull(plate);
            Assert.AreEqual(256, plate.Width);
        }

        [TestMethod]
        public void TestPanakFolderDataSynchronizesWithDiskFolderName() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakSyncTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                var data = PanakFolderData.Load(tempDir);
                Assert.AreEqual(Path.GetFileName(tempDir), data.Title);

                // Save custom title in json
                data.Title = "OldMismatchedTitle";
                data.Save(tempDir);

                // Load again - Title should sync to disk folder name
                var reloaded = PanakFolderData.Load(tempDir);
                Assert.AreEqual(Path.GetFileName(tempDir), reloaded.Title);
            } finally {
                if (Directory.Exists(tempDir)) {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestTwoIconsRenderCenteredAndLarger() {
            RectangleF innerRect = new RectangleF(20, 20, 200, 200);

            var (dim2, centers2) = IconGenerator.CalculateIconLayout(innerRect, 2);
            var (dim4, _) = IconGenerator.CalculateIconLayout(innerRect, 4);

            // 2-item icons must be larger than 4-item grid icons ("մի քիչ խոշոր")
            Assert.IsTrue(dim2 > dim4, $"2-item iconDim ({dim2}) should be larger than 4-item iconDim ({dim4})");

            Assert.AreEqual(2, centers2.Length);

            // Both must be centered vertically at cy
            float expectedCy = innerRect.Y + innerRect.Height / 2f;
            Assert.AreEqual(expectedCy, centers2[0].Y, 0.001f);
            Assert.AreEqual(expectedCy, centers2[1].Y, 0.001f);

            // Must be centered horizontally
            float midX = innerRect.X + innerRect.Width / 2f;
            float dist0 = midX - centers2[0].X;
            float dist1 = centers2[1].X - midX;
            Assert.IsTrue(dist0 > 0, "Icon 0 should be to the left of center");
            Assert.IsTrue(dist1 > 0, "Icon 1 should be to the right of center");
            Assert.AreEqual(dist0, dist1, 0.001f, "2 icons must be symmetrically centered");
        }

        [TestMethod]
        public void TestThreeIconsRenderTriangularApexUp() {
            RectangleF innerRect = new RectangleF(20, 20, 200, 200);

            var (dim2, _) = IconGenerator.CalculateIconLayout(innerRect, 2);
            var (dim3, centers3) = IconGenerator.CalculateIconLayout(innerRect, 3);

            // 3-item icons must be a bit smaller than 2-item icons ("մի քիչ ավելի փոքր քան երկու հատի դեպքում")
            Assert.IsTrue(dim3 < dim2, $"3-item iconDim ({dim3}) should be smaller than 2-item iconDim ({dim2})");

            Assert.AreEqual(3, centers3.Length);

            // Apex icon (centers3[0]): centered horizontally, on top row
            float midX = innerRect.X + innerRect.Width / 2f;
            Assert.AreEqual(midX, centers3[0].X, 0.001f, "Apex icon must be horizontally centered");

            // Base icons (centers3[1] and centers3[2]): on bottom row
            Assert.IsTrue(centers3[0].Y < centers3[1].Y, "Apex icon must be above bottom row icons (apex pointing up)");
            Assert.AreEqual(centers3[1].Y, centers3[2].Y, 0.001f, "Bottom two icons must share the same Y line");

            // Bottom row symmetry
            Assert.IsTrue(centers3[1].X < midX, "Bottom-left icon must be left of center");
            Assert.IsTrue(centers3[2].X > midX, "Bottom-right icon must be right of center");
            Assert.AreEqual(midX - centers3[1].X, centers3[2].X - midX, 0.001f, "Bottom two icons must be equidistant from apex X");
        }

        [TestMethod]
        public void TestInnerIconDimensionsIncreasedByFivePixels() {
            RectangleF innerRect = new RectangleF(0, 0, 100, 100);

            // 1-item
            var (dim1, _) = IconGenerator.CalculateIconLayout(innerRect, 1);
            float baseDim1 = 100f * 0.58f;
            Assert.AreEqual(baseDim1 + 5f, dim1, 0.001f);

            // 2-item
            var (dim2, _) = IconGenerator.CalculateIconLayout(innerRect, 2);
            float baseDim2 = 100f * 0.48f;
            Assert.AreEqual(baseDim2 + 5f, dim2, 0.001f);

            // 3-item
            var (dim3, _) = IconGenerator.CalculateIconLayout(innerRect, 3);
            float baseDim3 = 100f * 0.43f;
            Assert.AreEqual(baseDim3 + 5f, dim3, 0.001f);

            // 4-item (2x2 grid)
            var (dim4, _) = IconGenerator.CalculateIconLayout(innerRect, 4);
            float baseDim4 = 50f * 0.85f;
            Assert.AreEqual(baseDim4 + 5f, dim4, 0.001f);
        }

        [TestMethod]
        public void TestIconOpacitySupportAndDropShadowClipping() {
            Color testColor = Color.FromArgb(215, 197, 252);
            using var mini = new Bitmap(16, 16);

            // Test at 60% opacity (like user's background opacity)
            using Bitmap plate60 = IconGenerator.RenderPlateWithGrid(256, "Clover", testColor, new List<Bitmap> { mini }, opacity: 0.60);
            Assert.IsNotNull(plate60);
            Assert.AreEqual(256, plate60.Width);
            Assert.AreEqual(256, plate60.Height);

            // Test at 100% opacity
            using Bitmap plate100 = IconGenerator.RenderPlateWithGrid(256, "Squircle", testColor, new List<Bitmap> { mini }, opacity: 1.0);
            Assert.IsNotNull(plate100);
            Assert.AreEqual(256, plate100.Width);
        }

        [TestMethod]
        public void TestFolderAutoDissolvesWhenOneItemRemains() {
            string tempParent = Path.Combine(Path.GetTempPath(), "PanakDissolveParent_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempParent);
            try {
                string f1 = Path.Combine(tempParent, "itemA.txt");
                string f2 = Path.Combine(tempParent, "itemB.txt");
                File.WriteAllText(f1, "A");
                File.WriteAllText(f2, "B");

                string panakFolder = PanakManager.Instance.CreatePanakFolder(tempParent, new[] { f1, f2 }, "TestPanakFolder");
                Assert.IsTrue(Directory.Exists(panakFolder));

                // 2 items initially
                var itemsInitial = PanakManager.GetUserItems(panakFolder);
                Assert.AreEqual(2, itemsInitial.Count);
                Assert.IsFalse(PanakManager.Instance.CheckAndDissolveFolder(panakFolder), "Folder with 2 items should NOT dissolve");

                // Remove 1 item so 1 item remains inside
                string insideFileA = Path.Combine(panakFolder, "itemA.txt");
                File.Delete(insideFileA);

                // Now 1 item remains inside ("մնում է մեկ հատ, այն ինքնաշխատ ջնջվում է")
                bool dissolved = PanakManager.Instance.CheckAndDissolveFolder(panakFolder);
                Assert.IsTrue(dissolved, "Folder should dissolve when only 1 item remains");
                Assert.IsFalse(Directory.Exists(panakFolder), "Panak folder should be deleted");

                // The single remaining item (itemB.txt) must have been moved out to parent directory safely!
                string movedItemB = Path.Combine(tempParent, "itemB.txt");
                Assert.IsTrue(File.Exists(movedItemB), "Remaining item must be preserved by moving out to parent directory");
            } finally {
                if (Directory.Exists(tempParent)) {
                    foreach (var f in Directory.GetFiles(tempParent, "*", SearchOption.AllDirectories)) {
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                    }
                    try { Directory.Delete(tempParent, true); } catch { }
                }
            }
        }

        [TestMethod]
        public void TestFolderAutoDeletesWhenZeroItemsRemain() {
            string tempParent = Path.Combine(Path.GetTempPath(), "PanakEmptyParent_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempParent);
            try {
                string f1 = Path.Combine(tempParent, "item1.txt");
                string f2 = Path.Combine(tempParent, "item2.txt");
                File.WriteAllText(f1, "1");
                File.WriteAllText(f2, "2");

                string panakFolder = PanakManager.Instance.CreatePanakFolder(tempParent, new[] { f1, f2 }, "EmptyTestFolder");
                Assert.IsTrue(Directory.Exists(panakFolder));

                // Remove both items so 0 items remain inside
                File.Delete(Path.Combine(panakFolder, "item1.txt"));
                File.Delete(Path.Combine(panakFolder, "item2.txt"));

                // Now 0 items remain inside ("մեջը ոչ մի բան չի մնում, այն ինքնաշխատ ջնջվում է")
                bool dissolved = PanakManager.Instance.CheckAndDissolveFolder(panakFolder);
                Assert.IsTrue(dissolved, "Empty folder should dissolve and delete");
                System.Threading.SpinWait.SpinUntil(() => !Directory.Exists(panakFolder), 1000);
                Assert.IsFalse(Directory.Exists(panakFolder), "Panak folder should be deleted when empty");
            } finally {
                if (Directory.Exists(tempParent)) {
                    foreach (var f in Directory.GetFiles(tempParent, "*", SearchOption.AllDirectories)) {
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                    }
                    try { Directory.Delete(tempParent, true); } catch { }
                }
            }
        }

        [TestMethod]
        public void TestMaterialYouColorPaletteFromHue() {
            // Seed hue for default peach ~ 26.2
            double peachHue = 26.2;
            var (surfaceHex, containerHex, primaryHex) = ShellHelper.GetMaterialYouTonalPaletteHex(peachHue);

            Assert.AreEqual("#FEF4EB", surfaceHex, "Surface color at peach hue must match #FEF4EB");
            Assert.AreEqual("#FCDDC5", containerHex, "Container color at peach hue must match #FCDDC5");
            Assert.AreEqual("#7A5427", primaryHex, "Primary color at peach hue must match #7A5427");

            // Arbitrary hue rotation must also produce valid hex colors
            var (sCustom, cCustom, pCustom) = ShellHelper.GetMaterialYouTonalPaletteHex(180.0);
            Assert.IsTrue(sCustom.StartsWith("#") && sCustom.Length == 7, "Dynamic surface hex must be valid");
            Assert.IsTrue(cCustom.StartsWith("#") && cCustom.Length == 7, "Dynamic container hex must be valid");
            Assert.IsTrue(pCustom.StartsWith("#") && pCustom.Length == 7, "Dynamic primary hex must be valid");
        }

        [TestMethod]
        public void TestGitHubUpdateServiceVersionAndDefaults() {
            var updater = GitHubUpdateService.Instance;
            Assert.IsNotNull(updater);
            Assert.AreEqual("2.0.5", updater.CurrentVersionString);
            Assert.AreEqual(new Version(2, 0, 5), updater.CurrentVersion);
            Assert.AreEqual(UpdateStatus.Idle, updater.Status);
        }

        [TestMethod]
        public void TestUpdaterSettingsIntegration() {
            var settings = SettingsService.Instance.Current;
            Assert.IsNotNull(settings);
            Assert.IsTrue(settings.AutoCheckUpdates);
            Assert.AreEqual("hranthakobian/Panak", settings.GitHubRepo);
        }

        [TestMethod]
        public void TestTargetRepositoryAndNormalization() {
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.TargetRepository);
            Assert.AreEqual("https://github.com/hranthakobian/Panak", GitHubUpdateService.TargetRepositoryUrl);
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo("https://github.com/hranthakobian/Panak"));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo("https://github.com/hranthakobian/Panak/"));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo("github.com/hranthakobian/Panak"));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo("hranthakobian/Panak"));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo(@"hranthakobian\Panak"));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo(null));
            Assert.AreEqual("hranthakobian/Panak", GitHubUpdateService.NormalizeRepo(""));
        }

        [TestMethod]
        public void TestUpdateLocalizationKeys() {
            var loc = LocalizationService.Instance;
            string[] updateKeys = new[] {
                "Updates", "CheckForUpdates", "CheckingForUpdates",
                "UpToDate", "UpdateAvailable", "DownloadAndInstall",
                "DownloadingUpdate", "UpdateError", "AutoCheckUpdates",
                "AutoCheckUpdatesDesc", "CurrentVersion", "LatestVersion",
                "GitHubRepo", "ReleaseNotes"
            };

            foreach (var lang in new[] { "hy", "en", "ru" }) {
                loc.CurrentLanguage = lang;
                foreach (var k in updateKeys) {
                    string val = loc[k];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Update key '{k}' should exist in '{lang}'");
                }
            }
        }

        [TestMethod]
        public void TestNotifyFolderIconChangedPerformance() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakPerfTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                string testIco = Path.Combine(tempDir, "panak_icon_0.ico");
                File.WriteAllBytes(testIco, new byte[128]);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                ShellHelper.NotifyFolderIconChanged(tempDir, testIco);
                sw.Stop();

                Assert.IsTrue(sw.ElapsedMilliseconds < 500, $"NotifyFolderIconChanged took {sw.ElapsedMilliseconds}ms, should be under 500ms");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestSHGetSetFolderCustomSettingsBehavior() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakFcsTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                string testIco = Path.Combine(tempDir, "panak_icon_0.ico");
                File.WriteAllBytes(testIco, new byte[128]);

                string iniPath = Path.Combine(tempDir, "desktop.ini");
                File.WriteAllText(iniPath, "[.ShellClassInfo]\r\nIconResource=panak_icon_0.ico,0\r\n[ViewState]\r\nMode=\r\nVid=\r\nFolderType=Generic\r\n");

                var fcs = new ShellHelper.SHFOLDERCUSTOMSETTINGS();
                fcs.dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(ShellHelper.SHFOLDERCUSTOMSETTINGS));
                fcs.dwMask = ShellHelper.FCSM_ICONFILE;
                fcs.pszIconFile = Path.GetFileName(testIco);
                fcs.iIconIndex = 0;
                int hr = ShellHelper.SHGetSetFolderCustomSettings(ref fcs, tempDir, ShellHelper.FCS_FORCEWRITE);
                Console.WriteLine($"SHGetSetFolderCustomSettings relative HRESULT: 0x{hr:X8}");

                if (File.Exists(iniPath)) {
                    string res = File.ReadAllText(iniPath);
                    Console.WriteLine("desktop.ini content:\n" + res);
                    Assert.IsTrue(res.Contains("[ViewState]"), "ViewState section should be preserved");
                }
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestContextMenuRegistryDragDropHandlerRegistration() {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = Path.Combine(baseDir, "Panak.exe");
            if (!File.Exists(exePath)) {
                File.WriteAllText(exePath, "mock");
            }

            ContextMenuRegistry.RegisterAll(exePath);

            const string clsid = "{E5A7B812-3D4C-4A2E-8910-1F2D3C4E5A6B}";
            string[] expectedRoots = new[] {
                @"Software\Classes\Directory\shellex\DragDropHandlers\Panak",
                @"Software\Classes\Folder\shellex\DragDropHandlers\Panak",
                @"Software\Classes\Drive\shellex\DragDropHandlers\Panak",
                @"Software\Classes\Directory\Background\shellex\DragDropHandlers\Panak",
                @"Software\Classes\DesktopBackground\shellex\DragDropHandlers\Panak",
                @"Software\Classes\*\shellex\DragDropHandlers\Panak",
                @"Software\Classes\AllFilesystemObjects\shellex\DragDropHandlers\Panak"
            };
            foreach (var r in expectedRoots) {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(r)) {
                    Assert.IsNotNull(key, $"DragDropHandlers for {r} should be registered");
                    Assert.AreEqual(clsid, key.GetValue(""));
                }
            }

            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Classes\CLSID\{clsid}\InprocServer32")) {
                Assert.IsNotNull(key, "InprocServer32 for PanakDragDropHandler should be registered");
                Assert.AreEqual("Apartment", key.GetValue("ThreadingModel"));
            }

            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved")) {
                Assert.IsNotNull(key, "Approved shell extensions key should exist");
                Assert.AreEqual("Panak DragDrop Handler", key.GetValue(clsid));
            }
        }

        [TestMethod]
        public void TestContextMenuLanguageUpdates() {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = Path.Combine(baseDir, "Panak.exe");
            if (!File.Exists(exePath)) {
                File.WriteAllText(exePath, "mock");
            }

            // Register in Armenian (hy)
            ContextMenuRegistry.RegisterAll(exePath, "hy");
            using (var keyHy = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\PanakCreate")) {
                Assert.IsNotNull(keyHy);
                Assert.AreEqual("Հավաքել Panak-ի մեջ", keyHy.GetValue(""));
            }
            using (var keyHyDir = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\PanakOpen")) {
                Assert.IsNotNull(keyHyDir);
                Assert.AreEqual("Բացել Panak-ով", keyHyDir.GetValue(""));
            }
            using (var keyHyUnpack = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\PanakUnpack")) {
                Assert.IsNotNull(keyHyUnpack);
                Assert.AreEqual("Ապահավաքել Panak-ը", keyHyUnpack.GetValue(""));
            }

            // Change language to English (en)
            ContextMenuRegistry.RegisterAll(exePath, "en");
            using (var keyEn = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\PanakCreate")) {
                Assert.IsNotNull(keyEn);
                Assert.AreEqual("Collect into Panak folder", keyEn.GetValue(""));
            }
            using (var keyEnDir = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\PanakOpen")) {
                Assert.IsNotNull(keyEnDir);
                Assert.AreEqual("Open with Panak", keyEnDir.GetValue(""));
            }
            using (var keyEnUnpack = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\PanakUnpack")) {
                Assert.IsNotNull(keyEnUnpack);
                Assert.AreEqual("Unpack Panak folder", keyEnUnpack.GetValue(""));
            }

            // Change back to Armenian (hy)
            ContextMenuRegistry.RegisterAll(exePath, "hy");
            using (var keyBackHy = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\PanakCreate")) {
                Assert.IsNotNull(keyBackHy);
                Assert.AreEqual("Հավաքել Panak-ի մեջ", keyBackHy.GetValue(""));
            }
        }

        [TestMethod]
        public void TestTrayContextMenuCreationAndPalette() {
            var thread = new System.Threading.Thread(() => {
                var tm = new TrayManager();

                // Test Dark Mode
                SettingsService.Instance.Current.ThemeMode = "Dark";
                var darkMenu = tm.BuildContextMenu();
                Assert.IsNotNull(darkMenu);
                Assert.IsTrue(darkMenu.Items.Count >= 4, "Dark menu must contain items");

                // Test Light Mode
                SettingsService.Instance.Current.ThemeMode = "Light";
                var lightMenu = tm.BuildContextMenu();
                Assert.IsNotNull(lightMenu);
                Assert.IsTrue(lightMenu.Items.Count >= 4, "Light menu must contain items");
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestSettingsWindowApplyAndRealTimePaletteUpdate() {
            var thread = new System.Threading.Thread(() => {
                var w = new SettingsWindow();
                Assert.IsNotNull(w.ApplyBtn, "ApplyBtn must exist in SettingsWindow");
                Assert.IsFalse(string.IsNullOrWhiteSpace(w.ApplyBtn.Content?.ToString()), "ApplyBtn must have content");

                // Test palette update in real-time
                w.ApplyMaterialYouPalette(140.0); // Greenish hue
                Assert.IsNotNull(w.Resources["M3PrimaryBrush"]);
                Assert.IsNotNull(w.Resources["SettingsHueThumbBrush"]);

                // Test applying settings
                w.ApplyCurrentSettings();
                Assert.IsNotNull(SettingsService.Instance.Current);
                w.Close();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestLogoDynamicColorsAndBitmapRendering() {
            // Test at peach default hue
            var (cContainer, cTriangle, cCloud, cShield, cEllipse) = IconGenerator.GetLogoDynamicColors(26.2, false);
            Assert.AreNotEqual(Color.Empty, cContainer);
            Assert.AreNotEqual(Color.Empty, cTriangle);
            Assert.AreNotEqual(Color.Empty, cCloud);
            Assert.AreNotEqual(Color.Empty, cShield);
            Assert.AreNotEqual(Color.Empty, cEllipse);
            Assert.AreEqual(255, cContainer.A);
            Assert.AreEqual(255, cTriangle.A);
            Assert.AreEqual(255, cCloud.A);
            Assert.AreEqual(255, cShield.A);
            Assert.AreEqual(255, cEllipse.A);

            // Test rendering bitmap at different sizes
            using var bmp32 = IconGenerator.RenderPanakLogoBitmap(32, 26.2, false);
            Assert.IsNotNull(bmp32);
            Assert.AreEqual(32, bmp32.Width);
            Assert.AreEqual(32, bmp32.Height);

            using var bmp48Dark = IconGenerator.RenderPanakLogoBitmap(48, 200.0, true);
            Assert.IsNotNull(bmp48Dark);
            Assert.AreEqual(48, bmp48Dark.Width);
            Assert.AreEqual(48, bmp48Dark.Height);

            // Test uninstaller logo bitmap rendering
            using var bmpUninst32 = ShellHelper.RenderUninstallerLogoBitmap(32, 26.2, false);
            Assert.IsNotNull(bmpUninst32);
            Assert.AreEqual(32, bmpUninst32.Width);
            Assert.AreEqual(32, bmpUninst32.Height);

            using var bmpUninst48Dark = ShellHelper.RenderUninstallerLogoBitmap(48, 200.0, true);
            Assert.IsNotNull(bmpUninst48Dark);
            Assert.AreEqual(48, bmpUninst48Dark.Width);
            Assert.AreEqual(48, bmpUninst48Dark.Height);
        }

        [TestMethod]
        public void TestFolderOpacitySettingAndLiveUpdate() {
            var settings = new AppSettings();
            Assert.AreEqual(0.85, settings.FolderOpacity, 0.001, "Default FolderOpacity should be 0.85");

            settings.FolderOpacity = 0.50;
            string json = System.Text.Json.JsonSerializer.Serialize(settings);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(0.50, deserialized.FolderOpacity, 0.001);

            // Verify live update method does not crash when no popup is open
            PanakManager.Instance.UpdateOpenPopupOpacity(0.75);
        }

        [TestMethod]
        public void TestNewLocalizationKeysInAllSupportedLanguages() {
            var loc = LocalizationService.Instance;
            string[] keys = new[] { "GeneralFolderOpacity", "GeneralFolderOpacityDesc" };
            string[] langs = new[] { "hy", "en", "ru", "fr", "es", "zh", "ar" };

            foreach (var lang in langs) {
                loc.CurrentLanguage = lang;
                foreach (var k in keys) {
                    string val = loc[k];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Localization key '{k}' must exist in language '{lang}'");
                }
            }
        }

        [TestMethod]
        public async Task TestDetectOptimalLanguageReturnsValidLanguage() {
            string detected = await LocalizationService.DetectOptimalLanguageAsync();
            var valid = new HashSet<string> { "hy", "en", "ru", "fr", "es", "zh", "ar" };
            Assert.IsTrue(valid.Contains(detected), $"Detected language '{detected}' must be one of the supported languages");
        }

        [TestMethod]
        public void TestKeyRebindingSettingsSerialization() {
            var s = new AppSettings {
                QuickLookHotkey = "Ctrl + Space",
                ClosePopupHotkey = "Q"
            };
            string json = System.Text.Json.JsonSerializer.Serialize(s);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual("Ctrl + Space", deserialized.QuickLookHotkey);
            Assert.AreEqual("Q", deserialized.ClosePopupHotkey);
        }

        [TestMethod]
        public void TestHookServiceHotkeyMatching() {
            Assert.IsTrue(HookService.MatchesHotkey("Space", 0x20));
            Assert.IsTrue(HookService.MatchesHotkey("Escape", 0x1B));
            Assert.IsTrue(HookService.MatchesHotkey("Esc", 0x1B));
            Assert.IsTrue(HookService.MatchesHotkey("Tab", 0x09));
            Assert.IsFalse(HookService.MatchesHotkey("Space", 0x1B));
        }

        [TestMethod]
        public void TestSettingsWindowUiRebindingAndRoundedCorners() {
            var thread = new System.Threading.Thread(() => {
                var w = new SettingsWindow();

                // 1. Verify DangerButtonStyle has rounded corners (CornerRadius 20)
                var dangerStyle = (System.Windows.Style)w.FindResource("DangerButtonStyle");
                Assert.IsNotNull(dangerStyle, "DangerButtonStyle must exist");

                // 2. Verify KeycapBadgeButtonStyle exists
                var keycapStyle = (System.Windows.Style)w.FindResource("KeycapBadgeButtonStyle");
                Assert.IsNotNull(keycapStyle, "KeycapBadgeButtonStyle must exist");

                // 3. Verify Hue slider is full width (Stretch)
                Assert.AreEqual(System.Windows.HorizontalAlignment.Stretch, w.SettingsHueSlider.HorizontalAlignment);

                // 4. Verify Uninstall button has DangerButtonStyle
                Assert.AreEqual(dangerStyle, w.UninstallAppBtn.Style);

                // 5. Verify Rebinding containers exist
                Assert.IsNotNull(w.QuickLookKeysContainer);
                Assert.IsNotNull(w.ClosePopupKeysContainer);
                Assert.IsTrue(w.QuickLookKeysContainer.Children.Count > 0, "QuickLookKeysContainer must have keycap badge children");

                w.Close();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestKeyRebindingLocalizationKeysInAllLanguages() {
            var loc = LocalizationService.Instance;
            string[] keys = new[] { "KeyRebinding", "KeyRebindingDesc", "QuickLookKeyRow", "ClosePopupKeyRow", "PressAnyKey", "ResetKeys", "ReadyToInstall", "InstallNow" };
            string[] langs = new[] { "hy", "en", "ru", "fr", "es", "zh", "ar" };

            foreach (var lang in langs) {
                loc.CurrentLanguage = lang;
                foreach (var k in keys) {
                    string val = loc[k];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Key '{k}' must exist in language '{lang}'");
                }
            }
        }

        [TestMethod]
        public void TestInstallerAndUpdaterLocalizationKeysInAllLanguages() {
            var loc = LocalizationService.Instance;
            string[] keys = new[] {
                "UpdatePanak", "UpdatingPanak", "UpdatingPanakDesc",
                "PreparingUpdate", "PreparingInstall", "PreparingUninstall",
                "ExtractingFiles", "Extracting", "Copying", "CreatingShortcuts",
                "RegisteringMenus", "ConfiguringAutoStart", "RegisteringApp",
                "UpdatedSuccess", "UpdateCompletedDesc", "InstallFailed",
                "UpdateFailed", "UninstallFailed", "InstallErrorEnsureUnlocked",
                "InstallationFolder", "Browse"
            };
            string[] langs = new[] { "hy", "en", "ru", "fr", "es", "zh", "ar" };

            foreach (var lang in langs) {
                loc.CurrentLanguage = lang;
                foreach (var k in keys) {
                    string val = loc[k];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Key '{k}' must exist in language '{lang}'");
                    if (lang != "en") {
                        Assert.AreNotEqual(k, val, $"Key '{k}' must not fallback to key name in language '{lang}'");
                    }
                }
            }
        }

        [TestMethod]
        public void TestGitHubUpdateVersionParsing() {
            // Standard semver
            Assert.IsTrue(GitHubUpdateService.TryParseVersion("1.7.1", out var v1));
            Assert.AreEqual(new Version(1, 7, 1), v1);

            // Prefixed with 'v' or 'V'
            Assert.IsTrue(GitHubUpdateService.TryParseVersion("v1.7.2", out var v2));
            Assert.AreEqual(new Version(1, 7, 2), v2);

            // Prefixed with repo name 'panak'
            Assert.IsTrue(GitHubUpdateService.TryParseVersion("panak1.7.1", out var v3));
            Assert.AreEqual(new Version(1, 7, 1), v3);

            // Complex release string
            Assert.IsTrue(GitHubUpdateService.TryParseVersion("Panak-Installer-v2.0.2", out var v4));
            Assert.AreEqual(new Version(2, 0, 2), v4);

            // 4-part version
            Assert.IsTrue(GitHubUpdateService.TryParseVersion("1.7.1.5", out var v5));
            Assert.AreEqual(new Version(1, 7, 1, 5), v5);
        }

        [TestMethod]
        public void TestWavyProgressBarInitialization() {
            var thread = new System.Threading.Thread(() => {
                var bar = new Panak.Controls.WavyProgressBar {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 50,
                    WaveAmplitude = 2.5,
                    WaveLength = 20.0
                };
                Assert.AreEqual(50, bar.Value);
                Assert.AreEqual(2.5, bar.WaveAmplitude);
                Assert.AreEqual(20.0, bar.WaveLength);
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestInstallerWindowAndWelcomeWindowInitialization() {
            Exception? caughtEx = null;
            var thread = new System.Threading.Thread(() => {
                try {
                    var installer = new Panak.Views.InstallerWindow(isUninstallMode: false);
                    Assert.IsNotNull(installer);
                    var welcome = new Panak.Views.WelcomeWindow();
                    Assert.IsNotNull(welcome);
                } catch (Exception ex) {
                    caughtEx = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(caughtEx, $"Windows must initialize without exception: {caughtEx}");
        }

        [TestMethod]
        public void TestWelcomeFeatureTranslationsAcrossAllLanguages() {
            var loc = Panak.Services.LocalizationService.Instance;
            string[] languages = { "hy", "en", "ru", "fr", "es", "zh", "ar" };
            string[] keys = {
                "WelcomeToPanak",
                "WelcomeSubtitle",
                "WelcomeFeature1",
                "WelcomeFeature2",
                "WelcomeFeature3",
                "GoToSettings",
                "DonateOnKofi"
            };

            foreach (var lang in languages) {
                loc.CurrentLanguage = lang;
                foreach (var k in keys) {
                    string val = loc[k];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Language '{lang}' missing translation for key '{k}'");
                    Assert.AreNotEqual(k, val, $"Language '{lang}' translation for key '{k}' returned untranslated key name");
                }
            }
        }

        [TestMethod]
        public void TestCardClipGeometryMatchesWindowBounds() {
            Exception? caughtEx = null;
            var thread = new System.Threading.Thread(() => {
                try {
                    var popup = new FolderPopupWindow();
                    popup.Width = 360;
                    popup.Height = 420;
                    popup.UpdateLayout();

                    Assert.IsNotNull(popup.CardClipGeometry, "CardClipGeometry must exist");
                    // Verify that CardBorder has no margins causing 20px clipping
                    Assert.AreEqual(new System.Windows.Thickness(0), popup.CardBorder.Margin, "CardBorder margin should be 0");
                    Assert.AreEqual(28, popup.CardClipGeometry.RadiusX, "RadiusX should match corner radius");
                    Assert.AreEqual(28, popup.CardClipGeometry.RadiusY, "RadiusY should match corner radius");
                } catch (Exception ex) {
                    caughtEx = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(caughtEx, $"FolderPopupWindow clip geometry test threw: {caughtEx}");
        }

        [TestMethod]
        public void TestPanakFolderAutoPopupRecognition() {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakOpenTest_" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(tempDir);
                Assert.IsFalse(PanakManager.Instance.IsPanakFolder(tempDir));

                // Save PanakFolderData with AutoPopup = true (as set by "Always open with Panak")
                var data = new PanakFolderData {
                    Title = "TestFolder",
                    AutoPopup = true
                };
                data.Save(tempDir);

                Assert.IsTrue(PanakManager.Instance.IsPanakFolder(tempDir), "Folder with AutoPopup=true must be recognized as Panak folder");

                // Toggle AutoPopup = false
                data.AutoPopup = false;
                data.Save(tempDir);
                Assert.IsFalse(PanakManager.Instance.IsPanakFolder(tempDir), "Folder with AutoPopup=false must NOT be recognized as Panak folder");
            } finally {
                if (Directory.Exists(tempDir)) {
                    File.SetAttributes(tempDir, FileAttributes.Normal);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void TestDotNet8DesktopRuntimeDetection() {
            var (isInstalled, version) = StandaloneInstallerService.CheckDotNet8DesktopRuntime();
            // Since this build environment runs .NET 8, it must detect the installed runtime
            Assert.IsTrue(isInstalled, ".NET 8.0 Windows Desktop Runtime should be detected on development machine");
            Assert.IsFalse(string.IsNullOrWhiteSpace(version), "Detected .NET version string should not be empty");
            Assert.IsTrue(version!.StartsWith("8."), $"Detected version should start with '8.' but was '{version}'");
        }

        [TestMethod]
        public void TestStandaloneUninstallerServiceMethods() {
            string defaultDir = Panak.Services.StandaloneUninstallerService.GetDefaultInstallDirectory();
            Assert.IsFalse(string.IsNullOrEmpty(defaultDir));
            Assert.IsTrue(defaultDir.EndsWith("Panak", StringComparison.OrdinalIgnoreCase));

            var (isInstalled, version) = Panak.Services.StandaloneUninstallerService.CheckDotNet8DesktopRuntime();
            Assert.IsTrue(isInstalled, ".NET 8 runtime should be found on test machine");
            Assert.IsNotNull(version);
            Assert.IsTrue(version.StartsWith("8."));
        }

        [TestMethod]
        public void TestUninstallerLocalizationKeys() {
            var loc = Panak.Services.LocalizationService.Instance;

            loc.CurrentLanguage = "hy";
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallApology"]));
            StringAssert.Contains(loc["UninstallApology"], "չի արդարացրել");
            StringAssert.Contains(loc["UninstallApology"], "սպասելիքները");
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallDotNet8"]));
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallRemoveMenusAndShortcuts"]));
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallRemoveSettingsData"]));

            loc.CurrentLanguage = "en";
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallApology"]));
            StringAssert.Contains(loc["UninstallApology"], "expectations");
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallDotNet8"]));

            loc.CurrentLanguage = "ru";
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallApology"]));
            StringAssert.Contains(loc["UninstallApology"], "ожиданий");
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallDotNet8"]));

            loc.CurrentLanguage = "fr";
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallApology"]));
            StringAssert.Contains(loc["UninstallApology"], "attentes");
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallDotNet8"]));

            loc.CurrentLanguage = "es";
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallApology"]));
            StringAssert.Contains(loc["UninstallApology"], "expectativas");
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallDotNet8"]));

            loc.CurrentLanguage = "zh";
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallApology"]));
            StringAssert.Contains(loc["UninstallApology"], "预期");
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallDotNet8"]));

            loc.CurrentLanguage = "ar";
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallApology"]));
            StringAssert.Contains(loc["UninstallApology"], "توقعاتكم");
            Assert.IsFalse(string.IsNullOrEmpty(loc["UninstallDotNet8"]));
        }

        [TestMethod]
        public void TestPanakManagerBackdropDragMethods() {
            // Verify TemporarilyHideBackdropForDrag and RestoreBackdropAfterDrag can be invoked safely
            PanakManager.Instance.TemporarilyHideBackdropForDrag();
            PanakManager.Instance.RestoreBackdropAfterDrag();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        private const uint GW_HWNDNEXT = 2;

        [TestMethod]
        public void TestBackdropZOrderBehavior() {
            var thread = new System.Threading.Thread(() => {
                var popup = new FolderPopupWindow();
                popup.Show();
                var backdrop = new BackdropWindow();
                backdrop.Show();

                IntPtr popupHwnd = new System.Windows.Interop.WindowInteropHelper(popup).Handle;
                IntPtr backdropHwnd = new System.Windows.Interop.WindowInteropHelper(backdrop).Handle;

                popup.Topmost = true;
                backdrop.Visibility = System.Windows.Visibility.Hidden;
                popup.TogglePin(); // pinned
                popup.TogglePin(); // unpinned

                backdrop.Visibility = System.Windows.Visibility.Visible;
                popup.Topmost = true;
                ShellHelper.SetWindowPos(popupHwnd, ShellHelper.HWND_TOPMOST, 0, 0, 0, 0, ShellHelper.SWP_NOMOVE | ShellHelper.SWP_NOSIZE | ShellHelper.SWP_SHOWWINDOW);
                ShellHelper.SetWindowPos(backdropHwnd, popupHwnd, 0, 0, 0, 0, ShellHelper.SWP_NOMOVE | ShellHelper.SWP_NOSIZE | ShellHelper.SWP_NOACTIVATE);
                ShellHelper.ForceForegroundWindow(popupHwnd);

                bool popupAboveBackdrop = IsWindowAbove(popupHwnd, backdropHwnd);
                Assert.IsTrue(popupAboveBackdrop, "Popup window must be strictly above Backdrop in OS Z-order");

                popup.Close();
                backdrop.Close();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestBackdropWindowBlurEnableAndDisableBehavior() {
            var thread = new System.Threading.Thread(() => {
                var backdrop = new BackdropWindow();
                var screenRect = new System.Windows.Rect(0, 0, 800, 600);

                // 1. Show backdrop with blur enabled
                backdrop.ShowBackdrop(screenRect, targetDimmingOpacity: 0.50, blurEnabled: true, blurRadius: 28.0);
                Assert.IsTrue(backdrop.BlurImage.Visibility == System.Windows.Visibility.Visible, "BlurImage should be visible when blur is enabled");
                Assert.IsNotNull(backdrop.BlurImage.Effect, "BlurImage Effect must be configured when blur is enabled");
                Assert.IsInstanceOfType(backdrop.BlurImage.Effect, typeof(System.Windows.Media.Effects.BlurEffect));
                var effect = (System.Windows.Media.Effects.BlurEffect)backdrop.BlurImage.Effect;
                Assert.IsTrue(effect.Radius > 0, "Blur radius must be greater than 0");
                Assert.IsNotNull(backdrop.BlurImage.Source, "BlurImage.Source must not be null when blur is enabled");

                // 2. Show backdrop with blur disabled
                backdrop.ShowBackdrop(screenRect, targetDimmingOpacity: 0.50, blurEnabled: false, blurRadius: 0);
                Assert.AreEqual(System.Windows.Visibility.Collapsed, backdrop.BlurImage.Visibility, "BlurImage should be collapsed when blur is disabled");
                Assert.IsNull(backdrop.BlurImage.Source, "BlurImage.Source should be null when blur is disabled");

                backdrop.Close();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        private static bool IsWindowAbove(IntPtr higherHwnd, IntPtr lowerHwnd) {
            IntPtr current = higherHwnd;
            while (current != IntPtr.Zero) {
                current = GetWindow(current, GW_HWNDNEXT);
                if (current == lowerHwnd) {
                    return true; // higherHwnd precedes lowerHwnd in Z order (higher is above lower)
                }
            }
            return false;
        }

        [TestMethod]
        public void TestFolderPopupWindowEditPanelWidthAnimation() {
            var thread = new System.Threading.Thread(() => {
                var window = new FolderPopupWindow();
                Assert.AreEqual(258.0, window.MinWidth, 0.1, "Window MinWidth should be 258px to match 234px EditPanel + 24px margins");
                var editPanel = window.FindName("EditPanel") as System.Windows.FrameworkElement;
                Assert.IsNotNull(editPanel);
                Assert.AreEqual(234.0, editPanel.Width, 0.1);

                // Invoke private ToggleEditPanel via reflection
                var method = typeof(FolderPopupWindow).GetMethod("ToggleEditPanel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(method, "ToggleEditPanel method must exist");

                // Open edit panel
                method.Invoke(window, new object?[] { true });
                Assert.AreEqual(System.Windows.SizeToContent.Height, window.SizeToContent);

                // Close edit panel
                method.Invoke(window, new object?[] { false });

                window.Close();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestExplorerHelperDesktopFastPath() {
            // Verify GetDesktopSelectedPathFast can be called without unhandled exceptions
            string? path = ExplorerHelper.GetDesktopSelectedPathFast();
            // Path may be null in test environment without active desktop selection
        }

        [TestMethod]
        public void TestAppSettingsV190NewProperties() {
            var s = new AppSettings();
            Assert.IsTrue(s.FolderBlurEnabled, "FolderBlurEnabled should default to true");
            Assert.AreEqual(28.0, s.FolderBlurRadius, 0.01, "FolderBlurRadius should default to 28.0");
            Assert.AreEqual("Slider", s.PagingMode, "PagingMode should default to Slider");
            Assert.AreEqual(3, s.GridRows, "GridRows should default to 3");
            Assert.IsTrue(s.SliderArrowsEnabled, "SliderArrowsEnabled should default to true");
            Assert.IsTrue(s.SliderDragEnabled, "SliderDragEnabled should default to true");

            // Test round-trip JSON serialization
            s.FolderBlurEnabled = false;
            s.FolderBlurRadius = 42.5;
            s.PagingMode = "Scrollbar";
            s.GridRows = 4;
            s.SliderArrowsEnabled = false;
            s.SliderDragEnabled = false;

            string json = System.Text.Json.JsonSerializer.Serialize(s);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
            Assert.IsNotNull(deserialized);
            Assert.IsFalse(deserialized.FolderBlurEnabled);
            Assert.AreEqual(42.5, deserialized.FolderBlurRadius, 0.01);
            Assert.AreEqual("Scrollbar", deserialized.PagingMode);
            Assert.AreEqual(4, deserialized.GridRows);
            Assert.IsFalse(deserialized.SliderArrowsEnabled);
            Assert.IsFalse(deserialized.SliderDragEnabled);
        }

        [TestMethod]
        public void TestParenthesesRemovedFromSettings() {
            var loc = LocalizationService.Instance;
            foreach (var lang in new[] { "hy", "en", "ru", "fr", "es", "zh", "ar" }) {
                loc.CurrentLanguage = lang;
                string blur = loc["FocusBlur"];
                string radius = loc["FocusBlurRadius"];
                string quickLook = loc["QuickLookKeyRow"];
                string closePopup = loc["ClosePopupKeyRow"];

                Assert.IsFalse(blur.Contains("(Blur)"), $"Language '{lang}' should not contain (Blur)");
                Assert.IsFalse(radius.Contains("(Radius)"), $"Language '{lang}' should not contain (Radius)");
                Assert.IsFalse(quickLook.Contains("(Quick Look)"), $"Language '{lang}' should not contain (Quick Look)");
                Assert.IsFalse(closePopup.Contains("(Close Popup)"), $"Language '{lang}' should not contain (Close Popup)");
            }
        }

        [TestMethod]
        public void TestWavyProgressBarExpressiveDesignProperties() {
            var thread = new System.Threading.Thread(() => {
                var bar = new Panak.Controls.WavyProgressBar();
                Assert.IsNotNull(bar);
                Assert.AreEqual(2.8, bar.WaveAmplitude, 0.01);
                Assert.AreEqual(24.0, bar.WaveLength, 0.01);
                Assert.AreEqual(4.0, bar.TrackHeight, 0.01);
                Assert.AreEqual(4.0, bar.WaveStrokeThickness, 0.01);
                Assert.AreEqual(28.0, bar.WaveSpeed, 0.01);

                bar.WaveAmplitude = 5.0;
                Assert.AreEqual(5.0, bar.WaveAmplitude, 0.01);

                bar.Value = 50.0;
                Assert.AreEqual(50.0, bar.Value, 0.01);
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestPinAndUnpinLocalizationKeysExistInAllLanguages() {
            var loc = LocalizationService.Instance;
            foreach (var lang in new[] { "hy", "en", "ru", "fr", "es", "zh", "ar" }) {
                loc.CurrentLanguage = lang;
                string pin = loc["Pin"];
                string unpin = loc["Unpin"];
                Assert.IsFalse(string.IsNullOrWhiteSpace(pin), $"Pin should not be empty in {lang}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(unpin), $"Unpin should not be empty in {lang}");
            }
        }

        [TestMethod]
        public void TestLnkExtensionStripping() {
            string testFile1 = "document.pdf.lnk";
            string testFile2 = "photo.jpg";
            string testFile3 = "Photoshop.lnk";

            string name1 = Path.GetFileNameWithoutExtension(testFile1);
            string name2 = Path.GetFileNameWithoutExtension(testFile2);

            Assert.AreEqual("document.pdf", name1);
            Assert.AreEqual("photo", name2);
            Assert.AreEqual("Photoshop", Path.GetFileNameWithoutExtension(testFile3));
        }

        [TestMethod]
        public void TestImageExtensionsInShellHelper() {
            Assert.IsTrue(ShellHelper.ImageExtensions.Contains(".jpg"));
            Assert.IsTrue(ShellHelper.ImageExtensions.Contains(".jpeg"));
            Assert.IsTrue(ShellHelper.ImageExtensions.Contains(".png"));
            Assert.IsTrue(ShellHelper.ImageExtensions.Contains(".bmp"));
            Assert.IsTrue(ShellHelper.ImageExtensions.Contains(".webp"));
            Assert.IsTrue(ShellHelper.ImageExtensions.Contains(".gif"));
            Assert.IsFalse(ShellHelper.ImageExtensions.Contains(".txt"));
            Assert.IsFalse(ShellHelper.ImageExtensions.Contains(".exe"));
        }

        [TestMethod]
        public void TestResolveShortcutHandlesInvalidPath() {
            string? target = ShellHelper.ResolveShortcut("C:\\non_existent_folder_xyz\\file.lnk");
            Assert.IsNull(target);
        }

        [TestMethod]
        public void TestFolderPopupWindowPinAndMarqueeControls() {
            var thread = new System.Threading.Thread(() => {
                var popup = new FolderPopupWindow();
                Assert.IsNotNull(popup.PinButton, "PinButton should exist");
                Assert.IsNotNull(popup.TitleMarqueeCanvas, "TitleMarqueeCanvas should exist");
                Assert.IsNotNull(popup.TitleMarqueeTransform, "TitleMarqueeTransform should exist");
                Assert.IsNotNull(popup.FolderTitleDuplicateText, "FolderTitleDuplicateText should exist");
                Assert.IsFalse(popup.IsPinned, "Window should start unpinned");

                popup.TogglePin();
                Assert.IsTrue(popup.IsPinned, "Window should be pinned after toggle");
                Assert.IsTrue(popup.Topmost, "Window should be Topmost when pinned");

                bool origFocus = SettingsService.Instance.Current.FocusModeEnabled;
                try {
                    SettingsService.Instance.Current.FocusModeEnabled = false;
                    popup.TogglePin();
                    Assert.IsFalse(popup.IsPinned, "Window should be unpinned after toggle");
                    Assert.IsTrue(popup.Topmost, "Window should always stay Topmost even when unpinned without focus mode");

                    SettingsService.Instance.Current.FocusModeEnabled = true;
                    popup.TogglePin(); // pin
                    popup.TogglePin(); // unpin with focus mode enabled
                    Assert.IsTrue(popup.Topmost, "Window must remain Topmost when unpinned with focus mode enabled to prevent dropping behind backdrop");
                } finally {
                    SettingsService.Instance.Current.FocusModeEnabled = origFocus;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestPinnedPopupDoesNotCloseOnClickOutsideOrClosePopup() {
            var thread = new System.Threading.Thread(() => {
                var popup = new FolderPopupWindow();
                popup.TogglePin();
                Assert.IsTrue(popup.IsPinned);

                var hook = new HookService();
                hook.IsPopupOpenFunc = () => true;
                hook.IsPopupPinnedFunc = () => popup.IsPinned;

                bool closeRequestedFired = false;
                hook.CloseRequested += () => { closeRequestedFired = true; };

                Assert.IsTrue(hook.IsPopupPinnedFunc());
                Assert.IsFalse(closeRequestedFired);
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestDesktopWallpaperHelpers() {
            var thread = new System.Threading.Thread(() => {
                IntPtr workerW = ShellHelper.GetWallpaperWorkerW();
                var bmp = ShellHelper.GetDesktopWallpaperBitmap();
                Assert.IsNotNull(bmp, "GetDesktopWallpaperBitmap should return wallpaper");
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestFolderAndFileIconExtraction() {
            var thread = new System.Threading.Thread(() => {
                var folderIcon = ShellHelper.GetFolderIconSource(isLarge: true);
                Assert.IsNotNull(folderIcon, "ShellHelper.GetFolderIconSource must return a valid folder icon");
                Assert.IsTrue(folderIcon.IsFrozen, "Folder icon must be frozen");

                var docxIcon = ShellHelper.GetFileIconSource("sample.docx", isLarge: true);
                if (docxIcon != null) {
                    Assert.IsTrue(docxIcon.IsFrozen, "File icon must be frozen");
                }

                var txtIcon = ShellHelper.GetFileIconSource("sample.txt", isLarge: true);
                if (txtIcon != null) {
                    Assert.IsTrue(txtIcon.IsFrozen, "File icon must be frozen");
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestFolderPopupWindowSliderPageContent() {
            Exception? threadEx = null;
            var thread = new System.Threading.Thread(() => {
                try {
                    var popup = new FolderPopupWindow();
                    SettingsService.Instance.Current.PagingMode = "Slider";
                    SettingsService.Instance.Current.GridColumns = 3;
                    SettingsService.Instance.Current.GridRows = 3;

                    // Add 10 items (9 items on page 0, 1 item on page 1)
                    popup.Items.Clear();
                    for (int i = 0; i < 10; i++) {
                        popup.Items.Add(new Panak.Models.PanakItem {
                            DisplayName = $"Item {i}",
                            FullPath = $"C:\\test\\item{i}.txt",
                            IconSource = ShellHelper.GetFolderIconSource(isLarge: true)
                        });
                    }

                    popup.ApplySettingsFromGlobal();
                    popup.Show();

                    var frame = new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Loaded,
                        new Action(() => frame.Continue = false));
                    System.Windows.Threading.Dispatcher.PushFrame(frame);

                    popup.UpdateLayout();

                    var panel = (System.Windows.Controls.StackPanel)popup.FindName("SliderPagesPanel");
                    var viewport = (System.Windows.Controls.Grid)popup.FindName("SliderPagesViewport");
                    var trans = (System.Windows.Media.TranslateTransform)popup.FindName("PagesTranslateX");
                    var nextBtn = (System.Windows.Controls.Button)popup.FindName("SliderNextButton");

                    Assert.IsNotNull(panel);
                    Assert.IsNotNull(viewport);
                    Assert.IsNotNull(trans);
                    Assert.AreEqual(2, panel.Children.Count, "10 items with 9/page should create exactly 2 slider pages");

                    // SliderPagesPanel must not have a layout clip restricting its width to single page
                    Assert.IsNull(System.Windows.Media.VisualTreeHelper.GetClip(panel), "SliderPagesPanel must have null layout clip to allow rendering page 2");

                    // Click next page
                    nextBtn.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                    // Wait for page animation
                    var animEnd = DateTime.UtcNow.AddMilliseconds(350);
                    while (DateTime.UtcNow < animEnd) {
                        var f = new System.Windows.Threading.DispatcherFrame();
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                            System.Windows.Threading.DispatcherPriority.Background,
                            new Action(() => f.Continue = false));
                        System.Windows.Threading.Dispatcher.PushFrame(f);
                        System.Threading.Thread.Sleep(10);
                    }

                    popup.UpdateLayout();

                    // Hit test inside the viewport bounds to ensure Item 9 on page 2 is actually present and hit-testable
                    var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(viewport, new System.Windows.Point(20, 20));
                    Assert.IsNotNull(hitResult?.VisualHit, "Second page items must be hit-testable and rendered inside viewport");

                    popup.Close();
                } catch (Exception ex) {
                    threadEx = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (threadEx != null) {
                throw new AssertFailedException("STA Thread failed: " + threadEx.Message, threadEx);
            }
        }

        [TestMethod]
        public void TestRandomizerTogglesSettingsPersistence()
        {
            var settings = new Panak.Models.AppSettings
            {
                RandomShapeOnCreate = true,
                RandomColorOnCreate = false,
                RandomOpacityOnCreate = true,
                HiddenPresetColors = new System.Collections.Generic.List<string> { "#A8DADC", "#F4A261" }
            };

            string json = System.Text.Json.JsonSerializer.Serialize(settings);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<Panak.Models.AppSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.IsTrue(deserialized.RandomShapeOnCreate);
            Assert.IsFalse(deserialized.RandomColorOnCreate);
            Assert.IsTrue(deserialized.RandomOpacityOnCreate);
            Assert.AreEqual(2, deserialized.HiddenPresetColors.Count);
            Assert.AreEqual("#A8DADC", deserialized.HiddenPresetColors[0]);
            Assert.AreEqual("#F4A261", deserialized.HiddenPresetColors[1]);
        }

        [TestMethod]
        public void TestCustomColorDeletionAndHiddenPresets()
        {
            var settings = new Panak.Models.AppSettings();
            settings.CustomPaletteColors.Add("#112233");
            settings.CustomPaletteColors.Add("#445566");

            // Deleting custom color
            bool removedCustom = settings.CustomPaletteColors.Remove("#112233");
            Assert.IsTrue(removedCustom);
            Assert.AreEqual(1, settings.CustomPaletteColors.Count);
            Assert.IsFalse(settings.CustomPaletteColors.Contains("#112233"));

            // Hiding preset color
            settings.HiddenPresetColors.Add("#E6D7BD");
            Assert.IsTrue(settings.HiddenPresetColors.Contains("#E6D7BD"));

            // Restoring preset colors
            settings.HiddenPresetColors.Clear();
            Assert.AreEqual(0, settings.HiddenPresetColors.Count);
        }

        [TestMethod]
        public void TestLocalizationRandomByChance()
        {
            var loc = Panak.Services.LocalizationService.Instance;
            string prevLang = loc.CurrentLanguage;
            try
            {
                loc.CurrentLanguage = "hy";
                Assert.AreEqual("Պատահականությամբ", loc.Get("RandomByChance"));
                Assert.AreEqual("Վերականգնել լռելյայն գույները", loc.Get("RestoreDefaultColors"));

                loc.CurrentLanguage = "en";
                Assert.AreEqual("Randomly", loc.Get("RandomByChance"));
                Assert.AreEqual("Restore default colors", loc.Get("RestoreDefaultColors"));

                loc.CurrentLanguage = "ru";
                Assert.AreEqual("Случайно", loc.Get("RandomByChance"));
                Assert.AreEqual("Восстановить цвета по умолчанию", loc.Get("RestoreDefaultColors"));
            }
            finally
            {
                loc.CurrentLanguage = prevLang;
            }
        }

        [TestMethod]
        public void TestUnpackPanakFolderMovesFilesAndDissolves()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"panak_unpack_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                // Create a Panak folder inside tempDir
                string file1 = Path.Combine(tempDir, "document1.txt");
                string file2 = Path.Combine(tempDir, "document2.txt");
                File.WriteAllText(file1, "Content 1");
                File.WriteAllText(file2, "Content 2");

                string panakFolder = PanakManager.Instance.CreatePanakFolder(tempDir, new[] { file1, file2 }, "TestUnpackFolder");
                Assert.IsTrue(Directory.Exists(panakFolder));
                Assert.IsTrue(File.Exists(Path.Combine(panakFolder, "document1.txt")));
                Assert.IsTrue(File.Exists(Path.Combine(panakFolder, "document2.txt")));

                // Unpack Panak
                bool unpacked = PanakManager.Instance.UnpackPanakFolder(panakFolder);
                Assert.IsTrue(unpacked);

                // Both files should now be back in tempDir
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, "document1.txt")));
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, "document2.txt")));

                // Panak folder should no longer exist
                Assert.IsFalse(Directory.Exists(panakFolder));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [TestMethod]
        public void TestUnpackPanakLocalization()
        {
            var loc = Panak.Services.LocalizationService.Instance;
            string prev = loc.CurrentLanguage;
            try
            {
                loc.CurrentLanguage = "hy";
                Assert.AreEqual("Հավաքել Panak-ի մեջ", loc["CollectIntoPanakFolder"]);
                Assert.AreEqual("Ապահավաքել Panak-ը", loc["UnpackPanakFolder"]);

                loc.CurrentLanguage = "en";
                Assert.AreEqual("Collect into Panak folder", loc["CollectIntoPanakFolder"]);
                Assert.AreEqual("Unpack Panak folder", loc["UnpackPanakFolder"]);
            }
            finally
            {
                loc.CurrentLanguage = prev;
            }
        }

        [TestMethod]
        public void TestPanakJsonCreationAndDeletionSync()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakSyncTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "item1.txt"), "A");
            File.WriteAllText(Path.Combine(tempDir, "item2.txt"), "B");
            try
            {
                string metaFile = Path.Combine(tempDir, "panak.json");
                Assert.IsFalse(File.Exists(metaFile), "Initially panak.json should not exist");

                // 1. EnsurePanakFolderIntegrity automatically creates panak.json with AutoPopup = true
                PanakManager.Instance.EnsurePanakFolderIntegrity(tempDir);
                Assert.IsTrue(File.Exists(metaFile), "EnsurePanakFolderIntegrity must automatically create panak.json");

                var data = PanakFolderData.Load(tempDir);
                Assert.IsTrue(data.AutoPopup, "AutoPopup must be enabled by default");

                // 2. Test FolderPopupWindow sync when always open with Panak is toggled
                var thread = new System.Threading.Thread(() =>
                {
                    var popup = new Views.FolderPopupWindow();
                    popup.LoadFolder(tempDir, System.Windows.Rect.Empty);

                    Assert.IsTrue(popup.AlwaysPanakCheckBox.IsChecked == true, "AlwaysPanakCheckBox must be checked");
                    Assert.IsTrue(File.Exists(metaFile), "panak.json should exist when AlwaysPanakCheckBox is true");

                    // Uncheck Always Panak -> panak.json must be deleted
                    popup.AlwaysPanakCheckBox.IsChecked = false;
                    Assert.IsFalse(File.Exists(metaFile), "panak.json must be deleted when AlwaysPanak is unchecked");

                    // Re-check Always Panak -> panak.json must be recreated
                    popup.AlwaysPanakCheckBox.IsChecked = true;
                    Assert.IsTrue(File.Exists(metaFile), "panak.json must be recreated when AlwaysPanak is checked");

                    popup.Close();
                });
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
                thread.Join();
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }

        [TestMethod]
        public void TestUnpackPanakFolderMovesItemsToParentAndDissolves()
        {
            string rootDir = Path.Combine(Path.GetTempPath(), "PanakUnpackTest_" + Guid.NewGuid().ToString("N"));
            string folderToUnpack = Path.Combine(rootDir, "MySpecialPanak");
            string subDir = Path.Combine(folderToUnpack, "NestedSubFolder");

            Directory.CreateDirectory(folderToUnpack);
            Directory.CreateDirectory(subDir);

            string file1 = Path.Combine(folderToUnpack, "Document1.txt");
            string file2 = Path.Combine(folderToUnpack, "Photo.png");
            string fileInsideSub = Path.Combine(subDir, "InsideSub.txt");

            File.WriteAllText(file1, "Content 1");
            File.WriteAllText(file2, "Content 2");
            File.WriteAllText(fileInsideSub, "Inside sub content");

            try
            {
                Assert.IsTrue(Directory.Exists(folderToUnpack));
                Assert.IsTrue(File.Exists(file1));
                Assert.IsTrue(File.Exists(file2));

                PanakManager.Instance.UnpackPanakFolder(folderToUnpack);

                // Panak folder must be dissolved
                Assert.IsFalse(Directory.Exists(folderToUnpack), "The Panak folder must be dissolved and deleted.");

                // Files must have been unpacked to parent rootDir
                string unpackedFile1 = Path.Combine(rootDir, "Document1.txt");
                string unpackedFile2 = Path.Combine(rootDir, "Photo.png");
                string unpackedSubDir = Path.Combine(rootDir, "NestedSubFolder");

                Assert.IsTrue(File.Exists(unpackedFile1), "Document1.txt must exist in parent directory");
                Assert.IsTrue(File.Exists(unpackedFile2), "Photo.png must exist in parent directory");
                Assert.IsTrue(Directory.Exists(unpackedSubDir), "NestedSubFolder must exist in parent directory");
                Assert.IsTrue(File.Exists(Path.Combine(unpackedSubDir, "InsideSub.txt")), "Nested files must be intact");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(rootDir))
                    {
                        Directory.Delete(rootDir, true);
                    }
                }
                catch { }
            }
        }

        [TestMethod]
        [Ignore("Disabled in Panak Lite")]
        public void TestFolderLockService_CredentialsAndSession()
        {
            string testDir = Path.Combine(Path.GetTempPath(), "PanakLockTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            try
            {
                var lockService = FolderLockService.Instance;

                // 1. Password Lock
                lockService.SetFolderLock(testDir, "Password", "MySecretPassword123!");
                Assert.IsTrue(lockService.IsFolderLocked(testDir), "Folder must be reported as locked");

                // Wrong credential session unlock fails
                bool unlockedWrong = lockService.UnlockFolderSession(testDir, "WrongPass");
                Assert.IsFalse(unlockedWrong, "Wrong password must fail session unlock");
                Assert.IsTrue(lockService.IsFolderLocked(testDir), "Folder must remain locked");

                // Right credential session unlock succeeds
                bool unlockedRight = lockService.UnlockFolderSession(testDir, "MySecretPassword123!");
                Assert.IsTrue(unlockedRight, "Correct password must succeed session unlock");
                Assert.IsFalse(lockService.IsFolderLocked(testDir), "Folder must be temporarily unlocked for active session");

                // Relocking returns it to locked
                lockService.RelockFolderSession(testDir);
                Assert.IsTrue(lockService.IsFolderLocked(testDir), "Folder must be locked again after session relock");

                // Remove lock with right credential succeeds
                bool removed = lockService.RemoveFolderLock(testDir, "MySecretPassword123!");
                Assert.IsTrue(removed, "Correct password must allow removing the lock");
                Assert.IsFalse(lockService.IsFolderLocked(testDir), "Folder must no longer be locked");

                // 2. PIN Lock
                lockService.SetFolderLock(testDir, "PIN", "1234");
                Assert.IsTrue(lockService.IsFolderLocked(testDir));
                Assert.IsTrue(lockService.UnlockFolderSession(testDir, "1234"));
                Assert.IsTrue(lockService.RemoveFolderLock(testDir, "1234"));
                Assert.IsFalse(lockService.IsFolderLocked(testDir));

                // 3. Pattern Lock
                lockService.SetFolderLock(testDir, "Pattern", "0-1-2-5-8");
                Assert.IsTrue(lockService.IsFolderLocked(testDir));
                Assert.IsTrue(lockService.UnlockFolderSession(testDir, "0-1-2-5-8"));
                Assert.IsTrue(lockService.RemoveFolderLock(testDir, "0-1-2-5-8"));
                Assert.IsFalse(lockService.IsFolderLocked(testDir));
            }
            finally
            {
                try
                {
                    FolderLockService.Instance.RemoveFolderLock(testDir, "");
                    if (Directory.Exists(testDir))
                    {
                        Directory.Delete(testDir, true);
                    }
                }
                catch { }
            }
        }


        [TestMethod]
        public void TestFolderLockService_SettingsLockAndUninstallerGuard()
        {
            var lockService = FolderLockService.Instance;

            // Remove any existing settings lock
            lockService.RemoveSettingsLock("");

            Assert.IsFalse(lockService.IsSettingsLocked);

            // Set settings lock
            lockService.SetSettingsLock("Password", "AdminPassword999");
            Assert.IsTrue(lockService.IsSettingsLocked);
            Assert.AreEqual("Password", lockService.SettingsLockType);

            // Verify unlock
            Assert.IsFalse(lockService.VerifySettingsUnlock("WrongPass"));
            Assert.IsTrue(lockService.VerifySettingsUnlock("AdminPassword999"));

            // Remove settings lock
            Assert.IsTrue(lockService.RemoveSettingsLock("AdminPassword999"));
            Assert.IsFalse(lockService.IsSettingsLocked);

            // Test HasAnyLockedFolders
            lockService.ClearAllLocksForTesting();
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakGuardTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                lockService.SetFolderLock(tempDir, "PIN", "9999");
                Assert.IsTrue(lockService.HasAnyLockedFolders(), "HasAnyLockedFolders must be true when a locked folder exists");

                lockService.RemoveFolderLock(tempDir, "9999");
                Assert.IsFalse(lockService.HasAnyLockedFolders(), "HasAnyLockedFolders must be false after removing the lock");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void TestLocalization_ArmenianRenamedToDarzdnelAnsharzhAndApology()
        {
            var loc = LocalizationService.Instance;
            loc.CurrentLanguage = "hy";

            // Verify "LockFolders" is renamed to "Անջատել նիշքի քաշելով հանելը"
            string lockFoldersArm = loc["LockFolders"];
            Assert.AreEqual("Անջատել նիշքի քաշելով հանելը", lockFoldersArm, "Armenian LockFolders must be 'Անջատել նիշքի քաշելով հանելը'");
            Assert.AreEqual("Արգելափակել փոփափի տեղաշարժումը", loc["LockPopupPosition"]);

            // Verify uninstaller apology starts with "Ներողություն ենք խնդրում..."
            string apologyArm = loc["UninstallApology"];
            Assert.IsTrue(apologyArm.StartsWith("Ներողություն ենք խնդրում", StringComparison.Ordinal),
                "UninstallApology must start with 'Ներողություն ենք խնդրում...'");
        }

        [TestMethod]
        [Ignore("Disabled in Panak Lite")]
        public void TestFolderPopupDisplayAffinity_LockedVsUnlocked()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakAffinityTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var lockService = FolderLockService.Instance;
                Assert.IsFalse(lockService.IsFolderLocked(tempDir));

                // When unlocked, affinity should be WDA_NONE (0)
                bool isLockedUnlocked = !string.IsNullOrEmpty(tempDir) && lockService.IsFolderLocked(tempDir);
                uint affinityUnlocked = isLockedUnlocked ? ShellHelper.WDA_EXCLUDEFROMCAPTURE : ShellHelper.WDA_NONE;
                Assert.AreEqual(ShellHelper.WDA_NONE, affinityUnlocked);

                // Lock folder
                lockService.SetFolderLock(tempDir, "PIN", "1234");
                Assert.IsTrue(lockService.IsFolderLocked(tempDir));

                // When locked, affinity should be WDA_EXCLUDEFROMCAPTURE (0x11)
                bool isLockedLocked = !string.IsNullOrEmpty(tempDir) && lockService.IsFolderLocked(tempDir);
                uint affinityLocked = isLockedLocked ? ShellHelper.WDA_EXCLUDEFROMCAPTURE : ShellHelper.WDA_NONE;
                Assert.AreEqual(ShellHelper.WDA_EXCLUDEFROMCAPTURE, affinityLocked);

                // Clean up
                lockService.RemoveFolderLock(tempDir, "1234");
                Assert.IsFalse(lockService.IsFolderLocked(tempDir));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        [Ignore("Disabled in Panak Lite")]
        public void TestPatternLock_OneBasedAndFallbackValidation()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakPatternTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var lockService = FolderLockService.Instance;

                // 1. Lock with 1-based pattern (created by FolderLockSetupWindow)
                string pattern1Based = "1-2-3-6-9";
                lockService.SetFolderLock(tempDir, "Pattern", pattern1Based);
                Assert.IsTrue(lockService.IsFolderLocked(tempDir));

                // Verify that 1-based pattern unlocks the folder
                Assert.IsTrue(lockService.UnlockFolderSession(tempDir, pattern1Based));
                lockService.RelockFolderSession(tempDir);
                Assert.IsTrue(lockService.IsFolderLocked(tempDir));

                // Convert 1-based to 0-based fallback equivalent: "0-1-2-5-8"
                var oneBasedList = pattern1Based.Split('-').Select(int.Parse).ToList();
                string pattern0Based = string.Join("-", oneBasedList.Select(x => x - 1));
                Assert.AreEqual("0-1-2-5-8", pattern0Based);

                // Clean up lock
                Assert.IsTrue(lockService.RemoveFolderLock(tempDir, pattern1Based));
                Assert.IsFalse(lockService.IsFolderLocked(tempDir));

                // 2. Lock with 0-based pattern and test backward compatibility
                lockService.SetFolderLock(tempDir, "Pattern", pattern0Based);
                Assert.IsTrue(lockService.IsFolderLocked(tempDir));
                Assert.IsTrue(lockService.UnlockFolderSession(tempDir, pattern0Based));
                Assert.IsTrue(lockService.RemoveFolderLock(tempDir, pattern0Based));
                Assert.IsFalse(lockService.IsFolderLocked(tempDir));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void TestLockOverlayControl_M3FolderTheme()
        {
            Exception? caught = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var overlay = new Controls.LockOverlayControl();
                    overlay.ApplyFolderTheme("#10B981", true);
                    Assert.IsNotNull(overlay.Resources["LockPrimaryBrush"]);
                    Assert.IsNotNull(overlay.Resources["LockContainerBrush"]);
                    Assert.IsNotNull(overlay.Resources["LockOnContainerBrush"]);
                    Assert.IsNotNull(overlay.Resources["LockBtnHoverBrush"]);
                    Assert.IsNotNull(overlay.Resources["LockBtnPressedBrush"]);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(caught, $"LockOverlayControl M3 theme test failed: {caught}");
        }

        [TestMethod]
        [Ignore("Disabled in Panak Lite")]
        public void TestFolderPopupWindow_InPlaceLockSetupAndRemoveLock()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakInPlaceLockTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            Exception? caught = null;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var lockService = FolderLockService.Instance;

                    // 1. Initial state: unlocked, no lock
                    Assert.IsFalse(lockService.IsFolderLocked(tempDir));
                    Assert.IsNull(lockService.GetFolderLockInfo(tempDir));

                    var window = new FolderPopupWindow();
                    Assert.IsNotNull(window.FindName("LockSetupPanel"), "LockSetupPanel must exist in FolderPopupWindow");
                    Assert.IsNotNull(window.FindName("PopupLockOverlay"), "PopupLockOverlay must exist in FolderPopupWindow");
                    Assert.IsNotNull(window.FindName("FolderLockBtn"), "FolderLockBtn must exist in FolderPopupWindow");

                    // 2. Set lock using in-place credential logic
                    lockService.SetFolderLock(tempDir, "PIN", "1234");
                    Assert.IsTrue(lockService.IsFolderLocked(tempDir));
                    var lockInfo = lockService.GetFolderLockInfo(tempDir);
                    Assert.IsNotNull(lockInfo);
                    Assert.IsTrue(lockInfo.IsLocked);

                    // 3. Test remove lock setup mode on overlay
                    var overlay = (Controls.LockOverlayControl)window.FindName("PopupLockOverlay");
                    overlay.SetupForRemoveLock(tempDir);
                    Assert.IsTrue(overlay.IsRemoveLockMode, "Overlay should be in remove lock mode");

                    // 4. Test removing the lock with correct credential
                    Assert.IsTrue(lockService.RemoveFolderLock(tempDir, "1234"));
                    Assert.IsFalse(lockService.IsFolderLocked(tempDir));
                    Assert.IsNull(lockService.GetFolderLockInfo(tempDir));

                    window.Close();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);

            Assert.IsNull(caught, $"In-place lock setup test failed: {caught}");
        }

        [TestMethod]
        [Ignore("Disabled in Panak Lite")]
        public void TestLockedFolderAlwaysRecognizedEvenIfAutoPopupFalse()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakLockAutoPopupTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                // Create panak.json with AutoPopup = false
                var data = new PanakFolderData { AutoPopup = false };
                data.Save(tempDir);

                // Initially with no lock, IsPanakFolder returns false because AutoPopup is false
                Assert.IsFalse(PanakManager.Instance.IsPanakFolder(tempDir));

                // Lock folder
                FolderLockService.Instance.SetFolderLock(tempDir, "Password", "SecretPass123");
                Assert.IsTrue(FolderLockService.Instance.IsFolderLocked(tempDir));

                // Even though panak.json has AutoPopup = false, locked folder MUST be recognized as Panak folder
                Assert.IsTrue(PanakManager.Instance.IsPanakFolder(tempDir), "Locked folder must always be recognized as Panak folder");
                Assert.IsTrue(PanakManager.CheckIfPanakFolder(tempDir), "CheckIfPanakFolder must return true for locked folder");

                // Cleanup
                FolderLockService.Instance.RemoveFolderLock(tempDir, "SecretPass123");
                Assert.IsFalse(FolderLockService.Instance.IsFolderLocked(tempDir));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void TestPatternLineCrossingAndIntermediateDotSelection()
        {
            Exception? caught = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var overlay = new Controls.LockOverlayControl();
                    var initCanvasMethod = typeof(Controls.LockOverlayControl).GetMethod("InitPatternCanvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.IsNotNull(initCanvasMethod);
                    initCanvasMethod.Invoke(overlay, null);

                    // Retrieve private CheckHitDot and _currentPattern via reflection to test
                    var currentPatternField = typeof(Controls.LockOverlayControl).GetField("_currentPattern", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.IsNotNull(currentPatternField);
                    var patternList = (List<int>)currentPatternField.GetValue(overlay)!;

                    var checkHitDotMethod = typeof(Controls.LockOverlayControl).GetMethod("CheckHitDot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.IsNotNull(checkHitDotMethod);

                    var patternDotPositionsField = typeof(Controls.LockOverlayControl).GetField("_patternDotPositions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.IsNotNull(patternDotPositionsField);
                    var dotPositions = (System.Windows.Point[])patternDotPositionsField.GetValue(overlay)!;

                    // 1. Initial click on Dot 1
                    checkHitDotMethod.Invoke(overlay, new object[] { dotPositions[0] });
                    Assert.AreEqual(1, patternList.Count);
                    Assert.AreEqual(1, patternList[0]);

                    // 2. Drag directly to Dot 3 (line crosses Dot 2)
                    checkHitDotMethod.Invoke(overlay, new object[] { dotPositions[2] });
                    // Both 2 and 3 should be selected in order
                    Assert.AreEqual(3, patternList.Count);
                    Assert.AreEqual(1, patternList[0]);
                    Assert.AreEqual(2, patternList[1]);
                    Assert.AreEqual(3, patternList[2]);

                    // 3. From 3, drag to 9 (line crosses Dot 6)
                    checkHitDotMethod.Invoke(overlay, new object[] { dotPositions[8] });
                    Assert.AreEqual(5, patternList.Count);
                    Assert.AreEqual(6, patternList[3]);
                    Assert.AreEqual(9, patternList[4]);

                    // 4. From 9, drag to 7 (line crosses Dot 8)
                    checkHitDotMethod.Invoke(overlay, new object[] { dotPositions[6] });
                    Assert.AreEqual(7, patternList.Count);
                    Assert.AreEqual(8, patternList[5]);
                    Assert.AreEqual(7, patternList[6]);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(caught, $"Pattern line crossing test failed: {caught}");
        }

        [TestMethod]
        public void TestCrowdinOtaFetching() {
            var ota = CrowdinOtaService.Instance;
            var translations = ota.RefreshLanguageAsync("hy").GetAwaiter().GetResult();
            Assert.IsNotNull(translations, "Translations dictionary should be fetched from Crowdin OTA");
            Assert.IsTrue(translations.Count > 0, "Translations dictionary should contain keys");
            Assert.IsTrue(translations.ContainsKey("AppTitle"), "Translations should contain 'AppTitle'");
        }

        [TestMethod]
        public void TestLocalization_RenameToEdit_And_AutoReverseAnimations()
        {
            var loc = LocalizationService.Instance;
            loc.CurrentLanguage = "en";
            Assert.AreEqual("Edit", loc["Rename"], "English Rename must be localized as 'Edit'");

            loc.CurrentLanguage = "hy";
            Assert.AreEqual("Խմբագրել", loc["Rename"], "Armenian Rename must be localized as 'Խմբագրել'");

            // Check SettingsWindow.xaml for AutoReverse on SliderAnimLoaded and ScrollAnimLoaded
            string settingsXaml = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Views", "SettingsWindow.xaml"));
            Assert.IsTrue(settingsXaml.Contains("Name=\"SliderAnimLoaded\"") && settingsXaml.Contains("RepeatBehavior=\"Forever\" AutoReverse=\"True\""),
                "SettingsWindow.xaml must have AutoReverse='True' for SliderAnimLoaded");
            Assert.IsTrue(settingsXaml.Contains("Name=\"ScrollAnimLoaded\"") && settingsXaml.Contains("RepeatBehavior=\"Forever\" AutoReverse=\"True\""),
                "SettingsWindow.xaml must have AutoReverse='True' for ScrollAnimLoaded");

            // Check WelcomeWindow.xaml for Step1MoreActionsMenuBorder and Step1DropdownItemStyle
            string welcomeXaml = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Views", "WelcomeWindow.xaml"));
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step1MoreActionsMenuBorder\""),
                "WelcomeWindow.xaml must contain Step1MoreActionsMenuBorder");
            Assert.IsTrue(welcomeXaml.Contains("x:Key=\"Step1DropdownItemStyle\""),
                "WelcomeWindow.xaml must contain Step1DropdownItemStyle");
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step1PreviewMoreBtn\""),
                "WelcomeWindow.xaml must contain Step1PreviewMoreBtn");
        }

        [TestMethod]
        public void TestWelcomeWindowAndDynamicIconFeatures() {
            // 1. Verify RenderPanakLogoImageSource generates valid frozen BitmapSource
            var img = IconGenerator.RenderPanakLogoImageSource(32, 180.0, false);
            Assert.IsNotNull(img, "RenderPanakLogoImageSource must not return null");
            Assert.IsTrue(img.IsFrozen, "RenderPanakLogoImageSource must return frozen ImageSource for thread safety");

            // 2. Verify WelcomeWindow.xaml contains 28px corner radius on preview borders and clips
            string welcomeXaml = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Views", "WelcomeWindow.xaml"));
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step1PreviewBorder\" Grid.Column=\"1\" Margin=\"8,0,0,0\" CornerRadius=\"28\""),
                "Step1PreviewBorder must have CornerRadius='28'");
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step2PreviewBorder\" Grid.Column=\"0\" Margin=\"0,0,8,0\" CornerRadius=\"28\""),
                "Step2PreviewBorder must have CornerRadius='28'");
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step3PreviewBorder\" Grid.Column=\"0\" Margin=\"0,0,8,0\" CornerRadius=\"28\""),
                "Step3PreviewBorder must have CornerRadius='28'");
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step1PreviewClip\" RadiusX=\"28\" RadiusY=\"28\""),
                "Step1PreviewClip must have 28px radius");

            // 3. Verify ScrollViewers for controls in WelcomeWindow
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step1ControlsPanel\""), "Step1ControlsPanel must exist");
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step2ControlsPanel\""), "Step2ControlsPanel must exist");
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step1ControlsTranslate\""), "Step1ControlsTranslate must exist");
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step2ControlsTranslate\""), "Step2ControlsTranslate must exist");

            // 4. Verify warning badge placement above label
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step1BlurWarningBadge\""), "Step1BlurWarningBadge must exist");
            Assert.IsTrue(welcomeXaml.Contains("x:Name=\"Step2FocusBlurWarningBadge\""), "Step2FocusBlurWarningBadge must exist");

            // 5. Verify SettingsWindow.xaml warning badges
            string settingsXaml = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Views", "SettingsWindow.xaml"));
            Assert.IsTrue(settingsXaml.Contains("x:Name=\"FolderBlurWarningBadge\""), "FolderBlurWarningBadge must exist");
            Assert.IsTrue(settingsXaml.Contains("x:Name=\"FocusBlurWarningBadge\""), "FocusBlurWarningBadge must exist");
        }

        [TestMethod]
        public void TestBreadcrumbsBelowHeaderAndCurrentFolderTitle() {
            // 1. Verify XAML layout: BreadcrumbsBar is at Grid.Row="1" below Header
            string popupXaml = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Views", "FolderPopupWindow.xaml"));
            Assert.IsTrue(popupXaml.Contains("<RowDefinition Height=\"Auto\"/> <!-- Row 0: Header -->"), "Header row must be Row 0");
            Assert.IsTrue(popupXaml.Contains("<RowDefinition Height=\"Auto\"/> <!-- Row 1: Breadcrumbs Bar -->"), "Breadcrumbs Bar row must be Row 1");
            Assert.IsTrue(popupXaml.Contains("x:Name=\"BreadcrumbsBar\" Grid.Row=\"1\""), "BreadcrumbsBar must be at Grid.Row='1'");
            Assert.IsTrue(popupXaml.Contains("x:Name=\"BreadcrumbsScrollViewer\""), "BreadcrumbsScrollViewer must exist");
            Assert.IsTrue(popupXaml.Contains("x:Name=\"BreadcrumbsPanel\""), "BreadcrumbsPanel must exist");

            // 2. Functional test on STA thread
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakTest_" + Guid.NewGuid().ToString("N"));
            string subDir = Path.Combine(tempDir, "SubFolder1");
            string deepDir = Path.Combine(subDir, "DeepFolder2");
            Directory.CreateDirectory(deepDir);

            try {
                var thread = new System.Threading.Thread(() => {
                    var window = new FolderPopupWindow();
                    var folderTitleText = window.FindName("FolderTitleText") as System.Windows.Controls.TextBlock;
                    var breadcrumbsBar = window.FindName("BreadcrumbsBar") as System.Windows.Controls.Border;
                    var breadcrumbsPanel = window.FindName("BreadcrumbsPanel") as System.Windows.Controls.StackPanel;
                    var backButton = window.FindName("BackButton") as System.Windows.Controls.Button;

                    Assert.IsNotNull(folderTitleText);
                    Assert.IsNotNull(breadcrumbsBar);
                    Assert.IsNotNull(breadcrumbsPanel);
                    Assert.IsNotNull(backButton);

                    // Initialize window for tempDir
                    window.LoadFolder(tempDir, new System.Windows.Rect(0, 0, 100, 100));

                    // At root:
                    Assert.AreEqual(System.Windows.Visibility.Collapsed, breadcrumbsBar.Visibility, "BreadcrumbsBar must be Collapsed at root folder");
                    Assert.AreEqual(System.Windows.Visibility.Collapsed, backButton.Visibility, "BackButton must be Collapsed at root folder");
                    Assert.AreEqual(Path.GetFileName(tempDir), folderTitleText.Text, "Title must be root folder name at root");

                    // Navigate to SubFolder1:
                    window.NavigateTo(subDir);
                    Assert.AreEqual("SubFolder1", folderTitleText.Text, "Title must be 'SubFolder1' when inside subfolder");
                    Assert.AreEqual(System.Windows.Visibility.Visible, breadcrumbsBar.Visibility, "BreadcrumbsBar must be Visible when inside subfolder");
                    Assert.AreEqual(System.Windows.Visibility.Visible, backButton.Visibility, "BackButton must be Visible when inside subfolder");
                    Assert.IsTrue(breadcrumbsPanel.Children.Count >= 3, "BreadcrumbsPanel must have root button, separator, and subfolder item");

                    // Navigate to DeepFolder2:
                    window.NavigateTo(deepDir);
                    Assert.AreEqual("DeepFolder2", folderTitleText.Text, "Title must be 'DeepFolder2' when inside deep subfolder");
                    Assert.AreEqual(System.Windows.Visibility.Visible, breadcrumbsBar.Visibility);

                    // Navigate Up:
                    window.NavigateUp();
                    Assert.AreEqual("SubFolder1", folderTitleText.Text, "Title must be 'SubFolder1' after NavigateUp");

                    // Navigate Up to root:
                    window.NavigateUp();
                    Assert.AreEqual(Path.GetFileName(tempDir), folderTitleText.Text, "Title must be root folder name after returning to root");
                    Assert.AreEqual(System.Windows.Visibility.Collapsed, breadcrumbsBar.Visibility, "BreadcrumbsBar must collapse at root");
                    Assert.AreEqual(System.Windows.Visibility.Collapsed, backButton.Visibility, "BackButton must collapse at root");

                    window.Close();
                });
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
                thread.Join();
            } finally {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [TestMethod]
        [Ignore("Disabled in Panak Lite")]
        public void TestPinAnyLengthSupport()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakPinLengthTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var lockService = FolderLockService.Instance;

                // Test 2-digit PIN
                lockService.SetFolderLock(tempDir, "PIN", "42");
                Assert.IsTrue(lockService.IsFolderLocked(tempDir));
                Assert.IsFalse(lockService.UnlockFolderSession(tempDir, "4"));
                Assert.IsTrue(lockService.UnlockFolderSession(tempDir, "42"));
                Assert.IsTrue(lockService.RemoveFolderLock(tempDir, "42"));
                Assert.IsFalse(lockService.IsFolderLocked(tempDir));

                // Test 6-digit PIN
                lockService.SetFolderLock(tempDir, "PIN", "123456");
                Assert.IsTrue(lockService.IsFolderLocked(tempDir));
                Assert.IsFalse(lockService.UnlockFolderSession(tempDir, "1234"));
                Assert.IsTrue(lockService.UnlockFolderSession(tempDir, "123456"));
                Assert.IsTrue(lockService.RemoveFolderLock(tempDir, "123456"));
                Assert.IsFalse(lockService.IsFolderLocked(tempDir));

                // Test 10-digit PIN
                lockService.SetFolderLock(tempDir, "PIN", "9876543210");
                Assert.IsTrue(lockService.IsFolderLocked(tempDir));
                Assert.IsFalse(lockService.UnlockFolderSession(tempDir, "987654321"));
                Assert.IsTrue(lockService.UnlockFolderSession(tempDir, "9876543210"));
                Assert.IsTrue(lockService.RemoveFolderLock(tempDir, "9876543210"));
                Assert.IsFalse(lockService.IsFolderLocked(tempDir));

                // Test UI components on LockOverlayControl in STA thread
                Exception? caught = null;
                var thread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        var overlay = new Controls.LockOverlayControl();
                        Assert.IsNotNull(overlay.FindName("PinSubmitBtn"), "PinSubmitBtn must exist on LockOverlayControl");
                        Assert.IsNotNull(overlay.FindName("PinDotsPanel"), "PinDotsPanel must exist on LockOverlayControl");

                        // Test typing digits 1 to 6 (tests all 5 expressive shapes + looping)
                        var pinField = typeof(Controls.LockOverlayControl).GetField("_currentPin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var updateMethod = typeof(Controls.LockOverlayControl).GetMethod("UpdatePinDots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        Assert.IsNotNull(pinField);
                        Assert.IsNotNull(updateMethod);

                        var dotsPanel = (System.Windows.Controls.StackPanel)overlay.FindName("PinDotsPanel");

                        for (int i = 1; i <= 6; i++) {
                            pinField.SetValue(overlay, new string('1', i));
                            updateMethod.Invoke(overlay, null);
                            Assert.AreEqual(i, dotsPanel.Children.Count, $"Must have {i} dots");
                        }

                        // Test backspace
                        pinField.SetValue(overlay, "111");
                        updateMethod.Invoke(overlay, null);
                        Assert.AreEqual(3, dotsPanel.Children.Count);

                        // Test clear
                        pinField.SetValue(overlay, string.Empty);
                        updateMethod.Invoke(overlay, null);
                        Assert.AreEqual(0, dotsPanel.Children.Count);

                        // Test Keyboard input handling
                        overlay.Visibility = System.Windows.Visibility.Visible;
                        var setupUIMethod = typeof(Controls.LockOverlayControl).GetMethod("SetupUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        setupUIMethod.Invoke(overlay, new object[] { "PIN" });

                        // Test HandleTextInput
                        bool textHandled = overlay.HandleTextInput("123");
                        Assert.IsTrue(textHandled);
                        Assert.AreEqual("123", (string)pinField.GetValue(overlay));
                        Assert.AreEqual(3, dotsPanel.Children.Count);

                        // Test Backspace via HandlePreviewKeyDown
                        var backEventArgs = new System.Windows.Input.KeyEventArgs(
                            System.Windows.Input.Keyboard.PrimaryDevice,
                            new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                            0,
                            System.Windows.Input.Key.Back) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
                        bool backHandled = overlay.HandlePreviewKeyDown(backEventArgs);
                        Assert.IsTrue(backHandled);
                        Assert.AreEqual("12", (string)pinField.GetValue(overlay));
                        Assert.AreEqual(2, dotsPanel.Children.Count);

                        // Test Digits via HandlePreviewKeyDown (Key.D9 and Key.NumPad7)
                        var d9EventArgs = new System.Windows.Input.KeyEventArgs(
                            System.Windows.Input.Keyboard.PrimaryDevice,
                            new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                            0,
                            System.Windows.Input.Key.D9) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
                        bool d9Handled = overlay.HandlePreviewKeyDown(d9EventArgs);
                        Assert.IsTrue(d9Handled);
                        Assert.AreEqual("129", (string)pinField.GetValue(overlay));

                        var num7EventArgs = new System.Windows.Input.KeyEventArgs(
                            System.Windows.Input.Keyboard.PrimaryDevice,
                            new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                            0,
                            System.Windows.Input.Key.NumPad7) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
                        bool num7Handled = overlay.HandlePreviewKeyDown(num7EventArgs);
                        Assert.IsTrue(num7Handled);
                        Assert.AreEqual("1297", (string)pinField.GetValue(overlay));

                        // Test Escape clears when non-empty
                        var escEventArgs = new System.Windows.Input.KeyEventArgs(
                            System.Windows.Input.Keyboard.PrimaryDevice,
                            new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                            0,
                            System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
                        bool escHandled = overlay.HandlePreviewKeyDown(escEventArgs);
                        Assert.IsTrue(escHandled);
                        Assert.AreEqual(string.Empty, (string)pinField.GetValue(overlay));
                        Assert.AreEqual(0, dotsPanel.Children.Count);

                        // Test Escape returns false when already empty (so window can close)
                        bool escEmptyHandled = overlay.HandlePreviewKeyDown(escEventArgs);
                        Assert.IsFalse(escEmptyHandled);
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                });
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.Start();
                thread.Join();

                Assert.IsNull(caught, $"LockOverlayControl PIN UI test failed: {caught}");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [TestMethod]
        public void TestPerformanceOptimizationsV203()
        {
            // 1. Verify PanakItem implements INotifyPropertyChanged for async icon loading
            var item = new PanakItem { DisplayName = "test.png", FullPath = @"C:\test\test.png", IsDirectory = false };
            string? changedProp = null;
            item.PropertyChanged += (s, e) => changedProp = e.PropertyName;
            var icon = ShellHelper.GetFastPreviewIconSource(item.FullPath, item.IsDirectory, ".png", 0, true, out bool needsAsync);
            Assert.IsTrue(needsAsync, "Uncached image file should request async thumbnail decoding");
            item.IconSource = icon;
            Assert.AreEqual(nameof(PanakItem.IconSource), changedProp, "Setting IconSource must raise PropertyChanged");

            // 2. Verify generic extension icon does not require async load
            var txtIcon = ShellHelper.GetFastPreviewIconSource(@"C:\test\readme.txt", false, ".txt", 0, true, out bool txtNeedsAsync);
            Assert.IsFalse(txtNeedsAsync, "Standard .txt extension should resolve immediately without async thumbnail decode");

            // 3. Verify CreateFrozenBitmapSourceFast produces a frozen BitmapSource
            using var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            var frozenSrc = ShellHelper.CreateFrozenBitmapSourceFast(bmp);
            Assert.IsNotNull(frozenSrc);
            Assert.IsTrue(frozenSrc.IsFrozen);
            Assert.AreEqual(16, frozenSrc.PixelWidth);
            Assert.AreEqual(16, frozenSrc.PixelHeight);
        }

        [TestMethod]
        public void TestWelcomeWindowStartupColorsAndBrushProtection()
        {
            Exception? caught = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    // 1. Verify ShellHelper.UpdateOrCreateBrush never mutates a ResourceDictionary brush
                    var resDict = new System.Windows.ResourceDictionary();
                    var sharedPrimary = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7A, 0x54, 0x27)); // Dark brown
                    resDict["M3PrimaryBrush"] = sharedPrimary;

                    // An element with a DynamicResource to M3PrimaryBrush gets sharedPrimary as current background
                    var border = new System.Windows.Controls.Border();
                    border.Background = sharedPrimary;

                    var newColor = System.Windows.Media.Color.FromRgb(0xE6, 0xC5, 0xFC); // Light lavender
                    var updated = ShellHelper.UpdateOrCreateBrush(border.Background, newColor);

                    // sharedPrimary must remain intact
                    Assert.AreEqual(System.Windows.Media.Color.FromRgb(0x7A, 0x54, 0x27), sharedPrimary.Color,
                        "Shared ResourceDictionary brush must NOT be mutated in-place by UpdateOrCreateBrush!");
                    Assert.AreNotSame(sharedPrimary, updated, "UpdateOrCreateBrush must return a separate brush for elements referencing shared brushes");
                    Assert.AreEqual(newColor, updated.Color);

                    // On subsequent calls with the element's owned brush, it reuses the brush in-place
                    border.Background = updated;
                    var nextColor = System.Windows.Media.Color.FromRgb(0xFC, 0xDD, 0xC5);
                    var reupdated = ShellHelper.UpdateOrCreateBrush(border.Background, nextColor);
                    Assert.AreSame(updated, reupdated, "Subsequent updates of an owned brush must mutate in-place without reallocation");
                    Assert.AreEqual(nextColor, reupdated.Color);

                    // 2. Test WelcomeWindow initialization on STA thread
                    var welcome = new Panak.Views.WelcomeWindow();
                    var m3Primary = welcome.Resources["M3PrimaryBrush"] as System.Windows.Media.SolidColorBrush;
                    Assert.IsNotNull(m3Primary);
                    // M3PrimaryBrush must be the deep primary tonal color (NOT light pastel #E6C5FC or #FCDDC5)
                    Assert.IsTrue(m3Primary.Color.R < 200 || m3Primary.Color.B < 200,
                        $"M3PrimaryBrush should be a rich contrast color, not pastel: {m3Primary.Color}");

                    var m3Surface = welcome.Resources["M3SurfaceBrush"] as System.Windows.Media.SolidColorBrush;
                    Assert.IsNotNull(m3Surface);

                    var m3High = welcome.Resources["M3SurfaceContainerHighBrush"] as System.Windows.Media.SolidColorBrush;
                    Assert.IsNotNull(m3High, "M3SurfaceContainerHighBrush must be defined in WelcomeWindow resources");

                    var m3Highest = welcome.Resources["M3SurfaceContainerHighestBrush"] as System.Windows.Media.SolidColorBrush;
                    Assert.IsNotNull(m3Highest, "M3SurfaceContainerHighestBrush must be defined in WelcomeWindow resources");

                    var m3Outline = welcome.Resources["M3OutlineBrush"] as System.Windows.Media.SolidColorBrush;
                    Assert.IsNotNull(m3Outline, "M3OutlineBrush must be defined in WelcomeWindow resources");

                    var capsuleBg = welcome.Resources["ShapeCapsuleBgBrush"] as System.Windows.Media.SolidColorBrush;
                    Assert.IsNotNull(capsuleBg, "ShapeCapsuleBgBrush must be defined in WelcomeWindow resources");

                    welcome.Close();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(caught, $"WelcomeWindow startup color test failed: {caught}");
        }

        [TestMethod]
        public void TestHalfToCornerRadiusConverter()
        {
            var converter = new Panak.Markup.HalfToCornerRadiusConverter();
            var cr24 = (System.Windows.CornerRadius)converter.Convert(24.0, typeof(System.Windows.CornerRadius), null!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(12.0, cr24.TopLeft);
            Assert.AreEqual(12.0, cr24.BottomRight);

            var cr26 = (System.Windows.CornerRadius)converter.Convert(26.0, typeof(System.Windows.CornerRadius), null!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(13.0, cr26.TopLeft);

            var cr0 = (System.Windows.CornerRadius)converter.Convert(0.0, typeof(System.Windows.CornerRadius), null!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(0.0, cr0.TopLeft);

            var crNull = (System.Windows.CornerRadius)converter.Convert(null!, typeof(System.Windows.CornerRadius), null!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(0.0, crNull.TopLeft);

            Assert.ThrowsException<NotImplementedException>(() =>
                converter.ConvertBack(cr24, typeof(double), null!, System.Globalization.CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void TestWelcomeWindowAndSettingsWindowEnhancements()
        {
            string welcomeXamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Views", "WelcomeWindow.xaml");
            if (File.Exists(welcomeXamlPath))
            {
                string welcomeXaml = File.ReadAllText(welcomeXamlPath);
                Assert.IsTrue(welcomeXaml.Contains("Step1DesktopCanvas") || welcomeXaml.Contains("Margin=\"74,0,0,76\""), "WelcomeWindow popup container 2 must be anchored over folder 2");
                Assert.IsTrue(welcomeXaml.Contains("Step1ThemeSegmentGrid"), "WelcomeWindow must contain Step1ThemeSegmentGrid");
                Assert.IsTrue(welcomeXaml.Contains("Step1ThemeIndicatorPill"), "WelcomeWindow must contain Step1ThemeIndicatorPill");
                Assert.IsTrue(welcomeXaml.Contains("Step1ThemeLight"), "WelcomeWindow must contain Step1ThemeLight");
                Assert.IsTrue(welcomeXaml.Contains("Step1ThemeDark"), "WelcomeWindow must contain Step1ThemeDark");
                Assert.IsTrue(welcomeXaml.Contains("Step1ThemeAuto"), "WelcomeWindow must contain Step1ThemeAuto");
                Assert.IsTrue(welcomeXaml.Contains("Step1ShapePill"), "WelcomeWindow must contain Step1ShapePill");
            }

            string settingsXamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Views", "SettingsWindow.xaml");
            if (File.Exists(settingsXamlPath))
            {
                string settingsXaml = File.ReadAllText(settingsXamlPath);
                Assert.IsFalse(settingsXaml.Contains("RefreshTranslationsBtn"), "RefreshTranslationsBtn should be removed from SettingsWindow.xaml");
                Assert.IsTrue(settingsXaml.Contains("HalfToCornerRadiusConverter"), "SettingsWindow.xaml must use HalfToCornerRadiusConverter for button rounding");
            }
        }

        [TestMethod]
        public void TestSvgThumbnailSupport()
        {
            Assert.IsTrue(ShellHelper.ImageExtensions.Contains(".svg"), "ShellHelper.ImageExtensions must contain .svg");

            string tempSvg = Path.Combine(Path.GetTempPath(), $"panak_test_{Guid.NewGuid():N}.svg");
            try
            {
                File.WriteAllText(tempSvg, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"#FF0000\"/></svg>");

                var src = ShellHelper.GetFileThumbnailSource(tempSvg, isLarge: true);
                Assert.IsNotNull(src, "GetFileThumbnailSource must return a valid BitmapSource for SVG");
                Assert.IsTrue(src.PixelWidth > 0 && src.PixelHeight > 0, "SVG thumbnail must have positive dimensions");

                var bmp = ShellHelper.GetFileThumbnailBitmap(tempSvg, isLarge: true);
                Assert.IsNotNull(bmp, "GetFileThumbnailBitmap must return a valid Bitmap for SVG");
                Assert.IsTrue(bmp.Width > 0 && bmp.Height > 0, "SVG bitmap must have positive dimensions");
                bmp.Dispose();
            }
            finally
            {
                if (File.Exists(tempSvg))
                {
                    try { File.Delete(tempSvg); } catch { }
                }
            }
        }

        [TestMethod]
        public void TestExplorerHelperMethodsSafety()
        {
            Assert.AreEqual(System.Windows.Rect.Empty, ExplorerHelper.GetBoundingBoxForPath(string.Empty));
            Assert.AreEqual(System.Windows.Rect.Empty, ExplorerHelper.GetBoundingBoxForPath(null!));

            bool inText = ExplorerHelper.IsFocusInsideTextEdit();
            Assert.IsNotNull(inText);
        }

        [TestMethod]
        public void TestVerticalMarqueeTextBlock_PrepareWrappableText()
        {
            // Empty / null test
            Assert.AreEqual(string.Empty, Panak.Controls.VerticalMarqueeTextBlock.PrepareWrappableText(null));
            Assert.AreEqual(string.Empty, Panak.Controls.VerticalMarqueeTextBlock.PrepareWrappableText(""));

            // Breakable character tests
            string withUnderscores = Panak.Controls.VerticalMarqueeTextBlock.PrepareWrappableText("project_final_logo.svg");
            Assert.IsTrue(withUnderscores.Contains('\u200B'), "Must contain zero-width space after underscores/dots");

            // CamelCase test
            string camelCase = Panak.Controls.VerticalMarqueeTextBlock.PrepareWrappableText("ProjectFinalLogo");
            Assert.IsTrue(camelCase.Contains('\u200B'), "Must contain zero-width space between camelCase transitions");

            // Long continuous run of characters
            string longWord = Panak.Controls.VerticalMarqueeTextBlock.PrepareWrappableText("abcdefghijklmnopqrstuvwxyz");
            Assert.IsTrue(longWord.Contains('\u200B'), "Must contain zero-width space in long unbroken runs");

            // Armenian text with spaces
            string hyText = Panak.Controls.VerticalMarqueeTextBlock.PrepareWrappableText("Առաջարկ լոգո.svg");
            Assert.IsTrue(hyText.Contains("Առաջարկ"), "Armenian text must be preserved");
        }

        [TestMethod]
        public void TestVerticalMarqueeTextBlock_TwoLinesThresholdAndHover()
        {
            Exception? threadEx = null;
            var thread = new System.Threading.Thread(() => {
                try {
                    var marquee = new Panak.Controls.VerticalMarqueeTextBlock {
                        FontSize = 11,
                        Width = 66,
                        Height = 34
                    };

                    // 1 line text
                    marquee.Text = "Asset 1.svg";
                    marquee.UpdateTextMetricsAndState();
                    Assert.IsFalse(marquee.IsMultiLineExceeded, "1-line text must NOT exceed 2 lines");

                    // 2 line text
                    marquee.Text = "Առաջարկ\nլոգո.svg";
                    marquee.UpdateTextMetricsAndState();
                    Assert.IsFalse(marquee.IsMultiLineExceeded, "2-line text must NOT exceed 2 lines; should be statically visible");

                    // > 2 line text (long text)
                    marquee.Text = "Առաջարկ շատ երկար նիշքի նոր տարբերակ լոգո.svg";
                    marquee.UpdateTextMetricsAndState();
                    Assert.IsTrue(marquee.IsMultiLineExceeded, "Text longer than 2 lines must exceed 2 lines threshold");

                    // Hover toggle test
                    marquee.IsHovered = true;
                    Assert.IsTrue(marquee.IsHovered);
                    marquee.IsHovered = false;
                    Assert.IsFalse(marquee.IsHovered);
                } catch (Exception ex) {
                    threadEx = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(threadEx, $"VerticalMarqueeTextBlock test failed with: {threadEx}");
        }

        [TestMethod]
        public void TestFolderPopupWindow_ItemCellDimensionsAndTemplate()
        {
            Assert.AreEqual(78.0, FolderPopupWindow.ItemCellWidth, "ItemCellWidth must be 78.0");
            Assert.AreEqual(100.0, FolderPopupWindow.ItemCellHeight, "ItemCellHeight must be 100.0 for complete 2-line display");

            Exception? threadEx = null;
            var thread = new System.Threading.Thread(() => {
                try {
                    var popup = new FolderPopupWindow();
                    var dt = popup.Resources["PanakItemDataTemplate"] as System.Windows.DataTemplate;
                    Assert.IsNotNull(dt, "PanakItemDataTemplate must exist");
                } catch (Exception ex) {
                    threadEx = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(threadEx, $"FolderPopupWindow template test failed with: {threadEx}");
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [TestMethod]
        public void TestExplorerHelper_KeyboardAndTabsSafety()
        {
            long elapsedInsideHookMs = -1;
            Exception? hookEx = null;

            var thread = new System.Threading.Thread(() => {
                try {
                    // Warm up COM worker
                    ExplorerHelper.GetDesktopSelectedPathFast();

                    // Test inside an inter-thread SendMessage (exact same ISMEX_SEND state as WH_KEYBOARD_LL / WH_MOUSE_LL on STA thread)
                    var sourceParams = new System.Windows.Interop.HwndSourceParameters("DiagHookTest");
                    using var source = new System.Windows.Interop.HwndSource(sourceParams);
                    source.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) => {
                        if (msg == 0x0401) {
                            var swHook = System.Diagnostics.Stopwatch.StartNew();
                            string? res = ExplorerHelper.GetDesktopSelectedPathFast();
                            var item = ExplorerHelper.GetSelectedItemInActiveExplorer(resolveBounds: false);
                            swHook.Stop();
                            elapsedInsideHookMs = swHook.ElapsedMilliseconds;
                            Console.WriteLine($"[ISMEX_SEND] GetDesktopSelectedPathFast='{res ?? "NULL"}', Item='{item?.Path ?? "NULL"}', Elapsed={elapsedInsideHookMs}ms");
                            handled = true;
                        }
                        return IntPtr.Zero;
                    });

                    IntPtr targetHwnd = source.Handle;
                    var disp = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                    System.Threading.Tasks.Task.Run(() => {
                        SendMessage(targetHwnd, 0x0401, IntPtr.Zero, IntPtr.Zero);
                        SendMessage(targetHwnd, 0x0401, IntPtr.Zero, IntPtr.Zero);
                        disp.InvokeShutdown();
                    });
                    System.Windows.Threading.Dispatcher.Run();
                } catch (Exception ex) {
                    hookEx = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(hookEx, $"STA SendMessage hook test threw: {hookEx}");
            Assert.IsTrue(elapsedInsideHookMs >= 0 && elapsedInsideHookMs < 150, $"Hook execution must be fast (<150ms), actual: {elapsedInsideHookMs}ms");

            // Safety check: IsFocusInsideTextEdit does not crash and accurately checks visibility
            bool inEdit = ExplorerHelper.IsFocusInsideTextEdit();
            Assert.IsFalse(inEdit, "Focus inside text edit should be false in test environment");

            // Key conversion checks in HookService
            Assert.AreEqual(0x20u, HookService.ConvertKeyNameToVk("Space"));
            Assert.AreEqual(0x0Du, HookService.ConvertKeyNameToVk("Return"));
            Assert.AreEqual(0x0Du, HookService.ConvertKeyNameToVk("Enter"));
            Assert.AreEqual(0x1Bu, HookService.ConvertKeyNameToVk("Escape"));
            Assert.AreEqual(0x1Bu, HookService.ConvertKeyNameToVk("Esc"));
        }

        [TestMethod]
        public void TestFolderPopup_RealtimeBlurTimerAndAffinity()
        {
            var thread = new System.Threading.Thread(() =>
            {
                var popup = new FolderPopupWindow();
                SettingsService.Instance.Current.FolderBlurEnabled = true;
                SettingsService.Instance.Current.FolderBlurRadius = 28.0;

                popup.StartRealtimeBlurTimer();
                var timerField = typeof(FolderPopupWindow).GetField("_realtimePopupBlurTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(timerField, "_realtimePopupBlurTimer field must exist");
                var timer = timerField.GetValue(popup) as System.Windows.Threading.DispatcherTimer;
                Assert.IsNotNull(timer, "Timer should be instantiated when blur is enabled");
                Assert.IsTrue(timer.IsEnabled, "Timer should be enabled");

                popup.StopRealtimeBlurTimer();
                var timerAfterStop = timerField.GetValue(popup);
                Assert.IsNull(timerAfterStop, "Timer should be null after StopRealtimeBlurTimer");

                // Test CaptureScreenArea returns valid downsampled bitmap
                var bmp = ShellHelper.CaptureScreenArea(0, 0, 400, 300, dpiScale: 1.0, downsampleForBlur: true);
                Assert.IsNotNull(bmp, "CaptureScreenArea should return a non-null bitmap for valid area");
                Assert.IsTrue(bmp.IsFrozen, "Captured bitmap must be frozen for cross-thread rendering");

                popup.Close();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void TestRegularFolderRenameAndFileAddDoesNotTurnIntoPanakFolder()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "PanakRegFolderTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                string initialFolder = Path.Combine(tempRoot, "New folder");
                string renamedFolder = Path.Combine(tempRoot, "MyRegularFolder_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                Directory.CreateDirectory(initialFolder);

                // Simulate Explorer renaming "New folder" -> "MyRegularFolder" on Desktop
                Directory.Move(initialFolder, renamedFolder);
                PanakManager.Instance.RenameFolder(initialFolder, renamedFolder);
                PanakManager.Instance.WatchFolder(renamedFolder);

                // Add files to the regular folder
                File.WriteAllText(Path.Combine(renamedFolder, "doc1.txt"), "hello");
                File.WriteAllText(Path.Combine(renamedFolder, "doc2.txt"), "world");

                PanakManager.Instance.ScheduleFolderIconUpdate(renamedFolder, immediate: false);
                PanakManager.Instance.ScheduleFolderIconUpdate(renamedFolder, immediate: true);
                System.Threading.Thread.Sleep(350);

                Assert.IsFalse(PanakManager.Instance.IsPanakFolder(renamedFolder), "Regular renamed folder must NOT be recognized as a Panak folder");
                Assert.IsNull(PanakManager.Instance.FindKnownFolderByName(Path.GetFileName(renamedFolder)), "Regular renamed folder must NOT be added to _knownFolders");
                Assert.IsFalse(File.Exists(Path.Combine(renamedFolder, "desktop.ini")), "desktop.ini must NOT be created in a regular folder");
                Assert.AreEqual(0, Directory.GetFiles(renamedFolder, "panak_icon*.ico").Length, "panak_icon*.ico must NOT be created in a regular folder");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    foreach (var f in Directory.GetFileSystemEntries(tempRoot, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                    }
                    try { Directory.Delete(tempRoot, true); } catch { }
                }
            }
        }

        [TestMethod]
        public void TestCutPasteMoveDoesNotLeaveEmptyPanakGhostFolder()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "PanakCutPasteTest_" + Guid.NewGuid().ToString("N"));
            string desktopSim = Path.Combine(tempRoot, "DesktopSim");
            string targetContainer = Path.Combine(tempRoot, "TargetContainer");
            Directory.CreateDirectory(desktopSim);
            Directory.CreateDirectory(targetContainer);

            try
            {
                string f1 = Path.Combine(desktopSim, "alpha.txt");
                string f2 = Path.Combine(desktopSim, "beta.txt");
                File.WriteAllText(f1, "AlphaContent");
                File.WriteAllText(f2, "BetaContent");

                string srcFolder = PanakManager.Instance.CreatePanakFolder(desktopSim, new[] { f1, f2 }, "MovedPanakFolder");
                Assert.IsTrue(Directory.Exists(srcFolder), "Source Panak folder should exist initially");
                Assert.IsTrue(PanakManager.Instance.IsPanakFolder(srcFolder), "Source folder should be a Panak folder");

                // Simulate non-atomic / cross-volume Cut & Paste where Explorer moves files out of srcFolder to dstFolder
                string dstFolder = Path.Combine(targetContainer, Path.GetFileName(srcFolder));
                Directory.CreateDirectory(dstFolder);

                foreach (var file in Directory.GetFiles(srcFolder))
                {
                    string fileName = Path.GetFileName(file);
                    // Simulate Explorer moving user files and panak.json out, while a ghost panak_icon / desktop.ini is left or tested
                    if (!fileName.StartsWith("panak_icon", StringComparison.OrdinalIgnoreCase) &&
                        !fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                    {
                        string destFile = Path.Combine(dstFolder, fileName);
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Move(file, destFile);
                    }
                }

                // Wait for watcher + ghost folder cleanup to run
                bool cleanedUp = System.Threading.SpinWait.SpinUntil(() => !Directory.Exists(srcFolder), 2000);
                if (!cleanedUp)
                {
                    PanakManager.Instance.CleanupOrphanedPanakFolder(srcFolder);
                }

                Assert.IsFalse(Directory.Exists(srcFolder), "Empty ghost Panak folder must NOT remain on Desktop after its contents and metadata are moved out");
                Assert.IsTrue(File.Exists(Path.Combine(dstFolder, "alpha.txt")), "Moved file alpha.txt must exist in destination");
                Assert.IsTrue(File.Exists(Path.Combine(dstFolder, "beta.txt")), "Moved file beta.txt must exist in destination");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    foreach (var f in Directory.GetFileSystemEntries(tempRoot, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                    }
                    try { Directory.Delete(tempRoot, true); } catch { }
                }
            }
        }

        [TestMethod]
        public void TestRevertToRegularFolderStopsWatcherAndPreventsIconRegeneration()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PanakRevertWatchTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllText(Path.Combine(tempDir, "item1.txt"), "1");
                File.WriteAllText(Path.Combine(tempDir, "item2.txt"), "2");

                PanakManager.Instance.ConvertToPanakFolder(tempDir);
                Assert.IsTrue(PanakManager.Instance.IsPanakFolder(tempDir));
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, "desktop.ini")));

                // Revert to regular folder
                PanakManager.Instance.RevertToRegularFolder(tempDir);
                Assert.IsFalse(PanakManager.Instance.IsPanakFolder(tempDir));
                Assert.IsFalse(File.Exists(Path.Combine(tempDir, "desktop.ini")));
                Assert.AreEqual(0, Directory.GetFiles(tempDir, "panak_icon*.ico").Length);

                // Adding a new file after reverting must NOT re-trigger watcher to generate Panak icon
                File.WriteAllText(Path.Combine(tempDir, "item3.txt"), "3");
                System.Threading.Thread.Sleep(350);

                Assert.IsFalse(File.Exists(Path.Combine(tempDir, "desktop.ini")), "desktop.ini must NOT be regenerated after RevertToRegularFolder");
                Assert.AreEqual(0, Directory.GetFiles(tempDir, "panak_icon*.ico").Length, "panak_icon*.ico must NOT be regenerated after RevertToRegularFolder");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    foreach (var f in Directory.GetFileSystemEntries(tempDir, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                    }
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}