# DoomDriver – Návod na ovládanie State Machine (Debug & Test Guide)

Tento dokument slúži ako rýchla referencia pre testovanie hernej slučky (Game State Machine) a ovládania v aktuálnej verzii hry.

---

## 🎮 Prehľad klávesových skratiek

| Kláves | Kedy funguje | Akcia / Účinok |
| :--- | :--- | :--- |
| **`Enter`** | V stave **OrderSelection** (Lobby) | **Spustenie objednávky (`StartDelivery`)** – auto sa rozbehne, prepne do `DeliveryActive`. |
| **`A` / `D`** alebo **`←` / `→`** | Iba počas **DeliveryActive** | **Zmena jazdného pruhu** (v lobby a pri ukončení je ovládanie zamknuté). |
| **`Space` (Držať)** | Iba počas **DeliveryActive** | **Vytiahnutie mobilu a scrollovanie reelsiek** – dopĺňa FOMO (+20/s), pustí video/zvuk, ale zakryje výhľad na cestu. |
| **`Space` (Pustiť)** | Iba počas **DeliveryActive** | **Sklopenie mobilu** – návrat pohľadu na cestu. FOMO začne klesať (-8/s). |
| **`C`** *(Debug)* | Iba počas **DeliveryActive** | **Dokončenie doručenia (`CompleteDelivery`)** – simuluje dosiahnutie cieľa trasy. Auto zabrzdí, prepne do `DeliveryCompleted`. |
| **`R`** *(Debug)* | V stave **DeliveryCompleted** alebo **DeliveryFailed** | **Návrat do lobby (`ReturnToOrderSelection`)** – resetuje HP auta na 100 %, vycentruje pozíciu, zmaže prekážky a vráti kameru na mobil. |
| **`ESC`** | Kedykoľvek počas hry | **Pauza (`TogglePause`)** – zmrazí čas (`timeScale = 0`), zastaví počítadlá a utlmí zvuky. |

---

## 🔄 Jednotlivé stavy a ich správanie

### 1. `OrderSelection` (Lobby / Parkovisko)
* **Pohyb auta:** Auto stojí (`WorldMover.moveSpeed = 0`). Zmena pruhov a šmyky kolies sú zamknuté.
* **Kamera & Mobil:**
  * Mobil je vytiahnutý v ruke pred vodičom.
  * Kamera je naklonená dole o **17.6°** a priblížená na **FOV 40.8°** (prirodzený pohľad dole na displej).
  * Pozadie (volant, palubovka, mesto) je rozostrené pomocou **Depth of Field Blur** a jemne zotmavené boky (**Vignette 0.45**).
* **Zvuk & FOMO:**
  * Motor beží na tichom voľnobehu (pitch 0.8).
  * Zvuky notifikácií (Instagram, Messenger, iPhone) sú **úplne stíšené**.
  * FOMO meter je uzamknutý na 100 % (neklesá).
* **Ako pokračovať:** Stlač **`Enter`**.

---

### 2. `DeliveryActive` (Aktívne doručovanie / Jazda)
* **Pohyb auta:**
  * Svet sa dá do pohybu (rýchlosť štartuje na 12 m/s a postupne rastie).
  * V pravom hornom rohu sa začnú počítať metre (`xx m`).
  * Na ceste sa generujú prekážky (kužele, sudy, autá).
* **Kamera & Mobil:**
  * Ruka s mobilom sa automaticky sklopí dole.
  * Kamera sa vyrovná vodorovne na cestu a rozšíri záber na **FOV 60°**.
  * Blur pozadia sa okamžite vypne.
* **FOMO & Notifikácie:**
  * FOMO meter začne postupne ubúdať. Pri nízkom FOMO začne obrazovka tmavnúť (tunelové videnie).
  * Náhodne pípajú rušivé správy (notifikácie).
  * Podržaním **`Space`** vodič zodvihne mobil k tvári, čím doplní FOMO, no prestane vidieť na cestu.
* **Prekážky & Poškodenie:**
  * Náraz do prekážky uberie HP a spomalí auto.
  * Pri poklese HP na 0 auto vybuchne a hra prejde do `DeliveryFailed`.
* **Ako pokračovať:**
  * Úspech (debug): Stlač **`C`**.
  * Neúspech: Nechaj auto rozbiť na prekážkach (alebo počkaj na vypršanie limitu).

---

### 3. `DeliveryCompleted` (Úspešné doručenie)
* **Pohyb auta:** Auto plynule spomalí a zastaví na 0 m/s.
* **Dáta & Peňaženka:**
  * Zobrazí sa finálna prejdená vzdialenosť.
  * Odmena za doručenie sa pripíše do zostatku peňaženky (`PlayerBalance` uložená v `PlayerPrefs`).
  * Všetky zostávajúce prekážky na scéne sa automaticky odstránia.
* **Ako pokračovať:** Stlač **`R`** pre návrat do lobby k ďalšej zákazke.

---

### 4. `DeliveryFailed` (Zlyhanie / Havária)
* **Pohyb auta:** Auto okamžite stojí na mieste.
* **Efekty:** Dym a plamene z motora, syčanie chladiča.
* **Ako pokračovať:** Stlač **`R`** – auto sa kompletne opraví (100 HP, dym zhasne) a vrátiš sa do lobby (`OrderSelection`).

