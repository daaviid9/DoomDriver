# Game Jam Project – Vývojárska dokumentácia & Sprievodca projektom
---

## 1. Rýchly štart (Ako spustiť hru)

* **Render Pipeline:** Universal Render Pipeline (URP).
* **Vstupný systém:** Unity New Input System.

### Scény na spustenie:
1. **Hlavný chod hry (odporúčané):**
   * Otvor scénu: `Assets/_Game/Scenes/LoadingScreenMenu.unity`
   * Stlač **Play** ▶️.
   * Uvidíš načítavaciu obrazovku / hlavné menu so zoomom, hudbou a tlačidlom na štart.
2. **Priame testovanie gameplayu:**
   * Otvor scénu: `Assets/_Game/Scenes/EnviromentScene.unity`
   * Stlač **Play** ▶️ pre okamžitý skok do hry za volant.
3. **Izolované testy:**
   * V `Assets/_Game/Scenes/_Tests/` nájdeš scény slúžiace na ladené pokusy (častice, zvuky, testovacie prototypy), kde môžeš experimentovať bez ovplyvnenia hlavnej hry.

### Ovládanie pri testovaní v editore:
* **A / D** alebo **Šípky vľavo / vpravo** – zmena jazdného pruhu (vľavo, stred, vpravo).
* **Medzerník (Space / Jump)** – držaním zdvihneš mobil, pozeráš reelska a dobíjaš FOMO meter.
* **Escape** – pozastavenie hry (Pause menu).

---

## 2. Organizácia projektu a štruktúra priečinkov

Celý priečinok `Assets/` je rozdelený na dve hlavné časti: **náš vlastný kód a assety (`_Game/`)** a **stiahnuté asset balíčky (`ThirdParty/`)**.

```text
Assets/
├── _Game/                      <-- VŠETOK OBSAH VYTVORENÝ NAŠÍM TÍMOM
│   ├── Audio/                  <-- Zvukové efekty (autá, nárazy, notifikácie, menu)
│   ├── Data/
│   │   └── Input/              <-- Nastavenia New Input Systemu (.inputactions)
│   ├── Materials/              <-- Herné materiály (interiér, svetlá, efekty)
│   ├── Models/                 <-- 3D modely hry (interiér auta, tachometer)
│   ├── Prefabs/                <-- Prefaby rozdelené podľa kategórie:
│   │   ├── Cars/               <-- Premávka (ambulancia, polícia, taxík, autá...)
│   │   ├── Core/               <-- Manažérske prefaby (AudioManager...)
│   │   ├── Environment/        <-- Úseky cesty (RoadChunk_01, 02, 03)
│   │   ├── Obstacles/          <-- Prekážky (kužele, zátarasy, smetiaky)
│   │   └── Player/             <-- Prefab hráča (carInterior)
│   ├── Reels/                  <-- Brainrot videá (.mp4) a ich audio stopy (.mp3)
│   ├── RenderTextures/         <-- Render textúry pre displej telefónu
│   ├── Scenes/                 <-- Produkčné scény (LoadingScreenMenu, EnviromentScene)
│   │   └── _Tests/             <-- Pieskovisko pre testovacie scény
│   ├── Scripts/                <-- C# skripty kategorizované do podzložiek:
│   │   ├── Audio/              <-- Správa zvukov a hudby
│   │   ├── Core/               <-- GameManager, pohyb sveta, kamera
│   │   ├── Environment/        <-- Spawnovanie cesty a prekážok
│   │   ├── Phone/              <-- Logika telefónu, reelska a animácia ruky
│   │   ├── Player/             <-- Pohyb auta, kolízie a zdravie
│   │   └── UI/                 <-- Menu, healthbar, VHS filter a UI efekty
│   ├── Settings/               <-- URP assety a post-processing profily
│   ├── Textures/               <-- Textúry tachometra a budíkov
│   └── UI/
│       ├── Fonts/              <-- Písma a TextMeshPro font assety
│       ├── LoadingScreen/      <-- Pozadia načítavacej obrazovky (1.png - 5.png)
│       └── Sprites/            <-- UI sprity (ikona telefónu)
│
├── ThirdParty/                 <-- STIAHNUTÉ BALÍČKY Z ASSET STORE
│   ├── Customizable Skybox/
│   ├── FencesPack/
│   ├── [Free] Phone/
│   ├── Low Poly Simple Urban City 3D Asset Pack/
│   ├── Polygonal Particles/
│   ├── SimpleHands/
│   └── SimpleSky/
│
├── TextMesh Pro/               <-- Štandardné zdroje Unity balíčka TextMeshPro
└── TutorialInfo/               <-- Šablónové dáta
```

---

## 3. Dôležité tímové pravidlá (Git & Unity Workflow)

Aby sme predišli strate dát, ružovým materiálom (*Missing Material*) alebo nefunkčným skriptom (*Missing Script*), dodržiavaj tieto pravidlá:

### 1. Zlaté pravidlo `.meta` súborov
* V Unity má **každý súbor a priečinok** svoj vlastný `.meta` súbor s unikátnym GUID.
* **Nikdy nepresúvaj ani nemaž súbory cez prieskumník Windows bez ich `.meta` súboru!** Najlepšie je manipulovať so súbormi priamo v okne Project v Unity, ktoré presunie `.meta` automaticky.
* V Gite **vždy commituj `.meta` súbory spolu s ich assetmi**. Ak commitneš asset bez `.meta`, Unity mu na inom počítači vygeneruje nový GUID a prepojenia v scéne sa rozpadnú.

### 2. Čistota koreňového priečinka `Assets/`
* Do koreňa `Assets/` **nikdy neukladaj voľné súbory ani nové zložky**.
* Ak vytvoríš nový model, skript alebo textúru, ulož ju do príslušnej podzložky v `_Game/`.
* Ak stiahneš nový balíček z Asset Store, umiestni ho do `ThirdParty/`.

### 3. Predchádzanie Git konfliktom v scénach
* Scény v Unity (`.unity`) sa pri vetvení a súčasnej práci viacerých ľudí veľmi ťažko zlučujú (merge conflicts).
* **Pravidlo:** Ak pripravuješ novú funkcionalitu, auto, prekážku alebo UI prvok, **zabaľ ho do Prefabu** v `_Game/Prefabs/`. Do scény sa potom vloží iba hotový prefab, čo minimalizuje zmeny v samotnom súbore scény.
* Ak potrebuješ niečo len vyskúšať, použi alebo si vytvor novú scénu v `_Game/Scenes/_Tests/`.
