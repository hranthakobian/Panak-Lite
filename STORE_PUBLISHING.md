# Panak - Microsoft Store Submission Guide
### *Ինչպես հրապարակել Panak-ը Microsoft Store-ում (MSIX)*

Այս ուղեցույցը քայլ առ քայլ բացատրում է, թե ինչպես ստանալ Microsoft Partner Center-ի անհրաժեշտ անունները/տվյալները, ստեղծել Store-ի համար պատրաստի `.msix` փաթեթը և վերբեռնել այն Microsoft Store:

---

## Քայլ 1. Գրանցել հավելվածը Microsoft Partner Center-ում

1. Մուտք գործեք [Microsoft Partner Center (partner.microsoft.com)](https://partner.microsoft.com/dashboard).
2. Ձախ մենյուից ընտրեք **Apps and games** -> **New product** -> **MSIX or PWA app**:
3. Մուտքագրեք հավելվածի անունը՝ **Panak** (կամ ձեր նախընտրած անվանումը) և սեղմեք **Reserve product name**:
4. Բացված էջում, ձախ մենյուից գնացեք **Product management** -> **Product identity**:
5. Այստեղ կտեսնեք 3 կարևորագույն դաշտ.
   - **Package/Identity/Name** (օրինակ՝ `HrantHakobyan.Panak` կամ թվերով/տառերով նույնացուցիչ)
   - **Package/Identity/Publisher** (օրինակ՝ `CN=A1B2C3D4-E5F6-7890-ABCD-EF0123456789`)
   - **Package/Properties/PublisherDisplayName** (օրինակ՝ `Hrant Hakobyan`)

---

## Քայլ 2. Ավելացնել անունները `Package/MsixConfig.json` ֆայլում

Բացեք նախագծի [`Package/MsixConfig.json`](file:///c:/Users/hrant/Documents/GitHub/Panak/Package/MsixConfig.json) ֆայլը և Partner Center-ից պատճենված արժեքները տեղադրեք համապատասխան դաշտերում.

```json
{
  "Identity": {
    "Name": "ՏԵՂԱԴՐԵՔ_PACKAGE_IDENTITY_NAME",
    "Publisher": "CN=ՏԵՂԱԴՐԵՔ_PUBLISHER_ID",
    "PublisherDisplayName": "Hrant Hakobyan",
    "ProcessorArchitecture": "x64"
  },
  "Application": {
    "Id": "Panak",
    "DisplayName": "Panak",
    "Description": "Panak - Android-Style Popup Folders and Widgets for Windows",
    "TileShortName": "Panak",
    "BackgroundColor": "transparent"
  }
}
```

> [!TIP]
> Տարբերակը (`Version`) ավտոմատ կերպով կարդացվում է `Panak.csproj`-ից (օրինակ՝ `2.0.4` -> `2.0.4.0`), այնպես որ ամեն նոր թարմացման ժամանակ բավական է միայն փոխել տարբերակը `Panak.csproj`-ում:

---

## Քայլ 3. Կառուցել MSIX փաթեթը (Build)

Փաթեթը պատրաստելու համար կարող եք պարզապես երկու անգամ սեղմել [`scripts/BuildMsix.bat`](file:///c:/Users/hrant/Documents/GitHub/Panak/scripts/BuildMsix.bat) ֆայլի վրա կամ տերմինալից գործարկել.

```powershell
powershell -ExecutionPolicy Bypass -File "scripts\BuildMsix.ps1"
```

Սկրիպտը կատարում է հետևյալ գործողությունները.
1. Ավտոմատ գեներացնում է Microsoft Store-ի և Windows Tile-երի համար անհրաժեշտ բոլոր պատկերները (`Package/Assets/` թղթապանակում)՝ StoreLogo, Square44, Square71, Square150, Wide310, Square310, SplashScreen:
2. Թողարկում է (dotnet publish) Panak-ը ամբողջությամբ Self-Contained ռեժիմով (`win-x64`), որպեսզի Store-ից ներբեռնողի մոտ ոչ մի լրացուցիչ .NET Runtime-ի կարիք չլինի:
3. Ներառում է Windows Shell Drag & Drop ընդլայնումը (`Panak.ShellExt`):
4. Հավաքում է `.msix` ֆայլը [`publish/`](file:///c:/Users/hrant/Documents/GitHub/Panak/publish) թղթապանակում (օրինակ՝ `publish\Panak-v2.0.4.msix`):

---

## Քայլ 4. Տեղային թեստավորում (Local Testing - ըստ ցանկության)

Եթե ցանկանում եք փաթեթը փորձարկել և տեղադրել հենց ձեր Windows համակարգչի վրա նախքան Store ուղարկելը, գործարկեք ստորագրման ռեժիմով.

```powershell
powershell -ExecutionPolicy Bypass -File "scripts\BuildMsix.ps1" -SignForLocalTest
```

Սա կստեղծի տեղային զարգացման սերտիֆիկատ և կստորագրի MSIX-ը, որից հետո կարող եք երկու անգամ սեղմել `publish\Panak-v2.0.4.msix`-ի վրա և տեղադրել այն:

> [!NOTE]
> Microsoft Store վերբեռնելու համար ստորագրելու կարիք ՉԿԱ: Microsoft Store-ն ինքն է ավտոմատ ստորագրում փաթեթը պաշտոնական Microsoft վկայականով վերբեռնելուց հետո:

---

## Քայլ 5. Վերբեռնել Microsoft Partner Center (Submission)

1. Partner Center-ում սեղմեք **Start your submission**:
2. **Pricing and availability**: Ընտրեք Free (Անվճար) և ընտրեք այն երկրները, որտեղ հասանելի կլինի:
3. **Properties**:
   - Category: `Utilities & tools` կամ `Personalization`
   - Privacy policy URL: Ձեր GitHub էջի հղումը (օր. `https://github.com/hranthakobian/Panak`)
4. **Age ratings**: Լրացրեք կարճ հարցաշարը (հավելվածը հարմար է բոլոր տարիքի համար):
5. **Packages**:
   - Պարզապես Drag & Drop արեք պատրաստի `publish\Panak-v2.0.4.msix` ֆայլը:
   - Microsoft-ի ստուգիչ համակարգը ավտոմատ կճանաչի անվանումը, տարբերակը (`2.0.4.0`), լեզուները և `runFullTrust` հնարավորությունը:
6. **Store listings**:
   - Ավելացրեք նկարագրությունը (Description), սքրինշոթները և առանձնահատկությունները:
7. Սեղմեք **Submit to the Store**:

Շնորհավորում ենք, ձեր հավելվածը կանցնի Microsoft-ի արագ ստուգումը (Certification) և կհայտնվի Microsoft Store-ում:
