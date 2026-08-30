# sasSX — Ensamblador de z80 per a MSX

`sasSX` és un ensamblador de z80 de dues passades orientat a l'MSX: produeix
binaris plans, ROMs de cartutx (16K/32K amb capçalera `AB`) i megaROMs amb
mapejador, i entén una taula d'instruccions compatible amb la sintaxi d'**asMSX**.
És un fork de *SirCmpwn's Assembler*, ara com a projecte **.NET 10** que compila i
s'executa a Windows, Linux i macOS sense res més que el SDK de .NET.

---

## D'on ve i cap on va

**Origen — un ensamblador genèric.** *SirCmpwn's Assembler* (`sass`) va néixer dins
el projecte **KnightOS**, un sistema operatiu per a calculadores TI. Era un
ensamblador de z80 multiplataforma, de dues passades, escrit en C#, amb la sortida
pensada com un `.bin` pla: ni capçaleres, ni bancs, ni res específic de cap màquina.

**El gir a l'MSX (2016).** *Libertium Games* el va bifurcar, el va rebatejar
`sasSX` i el va orientar al cartutx d'MSX. Les peces que es van afegir:

- capçalera de ROM amb la signatura `AB` i punt d'entrada (`.rom` + `.start`);
- `.megarom` amb mapejadors (Konami, Konami SCC, ASCII8, ASCII16) i subpàgines;
- `.page` / `.subpage` per situar el codi als bancs;
- `.bios` per portar les etiquetes de la BIOS;
- `.module` / `.endmodule` per a etiquetes amb espai de noms;
- el mode `--asmsx`: taula d'instruccions alternativa (indirecció amb `[]` en lloc
  de `()`), `@@etiqueta` per a locals i sortida de símbols pensada per conviure amb
  **asMSX**, l'ensamblador de referència del món MSX.

El desenvolupament es va aturar cap a finals de 2016 amb uns quants ítems a mig fer
(vegeu el [full de ruta](#full-de-ruta)).

**La revifada (2026).** `sasSX` torna a moure's com a **motor de
[MSX Game Tools](https://github.com/eslamod/MSX-Game-Tools)**: l'editor exporta
`.asm` i el compara byte a byte amb el `.bin` que ell mateix genera, ensamblant-lo
amb `sasSX`; i les ROMs de prova de `msx/test_rom` es construeixen amb ell. De
passada s'ha modernitzat a un projecte **SDK-style de .NET 10** —el mateix SDK que
l'editor, sense Mono ni `xbuild`— i s'han corregit dues diferències de comportament
que .NET Core introduïa respecte del build antic (avaluació d'expressions negatives
i lectura parcial a `.incbin`).

L'objectiu recuperat és que `sasSX` sigui un ensamblador d'MSX **complet**: tancar
la megaROM, arreglar `.phase` i `.rept`, treure una taula de símbols en format
asMSX i un `--help` de debò.

---

## Estat actual

**Plataforma.** Projecte SDK-style de .NET 10. Compila i corre a Windows, Linux i
macOS només amb el SDK de .NET.

**Arquitectura.** z80, amb dues taules: l'estàndard i la de `--asmsx` (indirecció
amb `[]`). Insensible a majúscules i minúscules.

**Sortides.**

- Binari pla (`[fitxer].bin` per defecte, o `-` per treure-ho per la sortida
  estàndard).
- ROM de cartutx: amb `.rom` la sortida es genera en trams de 8K a partir de
  `0x4000`, i `.start` hi posa la capçalera de 16 bytes (`AB` + punter d'inici).
  Les ROMs de `msx/test_rom` en surten de 16384 o 32768 bytes exactes.
- Listing (`--listing`).
- Taula de símbols (`--symbols`), en format `.equ etiqueta 0xADREÇA` — **encara
  no** en format asMSX.

**Directives que funcionen.** `.org`, `.page`, `.rom`, `.start`, `.bios`,
`.incbin` (amb desplaçament i mida, i el PC s'actualitza), `.ds` / `.fill` (amb
límit de `0xFFFF`), `.db` / `.dw` / `.byte` / `.word` (amb nombres negatius),
`.equ`, `.define` / `.undefine`, `.macro`, `.module` / `.endmodule`, `.ascii` /
`.asciiz` / `.asciip`, `.if` / `.ifdef` / `.ifndef` / `.else` / `.elif` /
`.endif`, `.list` / `.nolist`, `.error`, `.end` / `.endfile`, comentaris de bloc
`/* */` i de línia `;` i `//`.

**A mig fer o amb errors coneguts.**

- `.megarom`: la detecció de mapejador té un error (només encerta uns quants
  noms), i el control de sobreeiximent de subpàgina només actua amb `.org`.
- `.phase` / `.dephase`: les referències absolutes dins del bloc no es
  reubiquen bé.
- `.rept` / `.endr`: no omple els bytes del bloc, que queden a `00`.
- `.block`: no té el límit de `0xFFFF` que sí tenen `.ds` i `.fill`.
- `echo`, `print`, `printdec`, `printtext`: de moment només donen el PC.
- `--help`: bolca una llista de fragments de codi, no ajuda de debò.
- Amb algunes entrades malformades (per exemple `.bios` sense trobar `bios.asm`)
  peta amb un `IndexOutOfRangeException` en lloc de donar un error net.
- `.basic`: no està implementada; l'ensamblador la reporta com a directiva no
  vàlida i continua.

**Proves.** El joc de `sass/tests/*.asm`, i com a validació externa les quatre
ROMs de `msx/test_rom` de MSX Game Tools: la sortida (ROM, símbols i listing) surt
idèntica byte a byte a la del build antic amb Mono.

---

## Compilació i execució

```sh
dotnet build -c Release
```

Surt `sass/bin/Release/sasSX` (`sasSX.exe` a Windows). També:

```sh
dotnet run --project sass -- [paràmetres] [fitxer.asm] [sortida]
```

`make` i `make install` són embolcalls prims de `dotnet build` i `dotnet publish`.

---

## Ús per línia de comandes

```
Usage: sasSX [paràmetres] [fitxer d'entrada] [fitxer de sortida]
```

El fitxer de sortida és opcional; si no s'indica, s'usa `[fitxer d'entrada].bin`.
Amb `-` com a sortida, el binari va a la sortida estàndard.

| Paràmetre | Àlies | Què fa |
| --- | --- | --- |
| `--asmsx` | `-as` | Taula d'instruccions alternativa i sintaxi compatible amb asMSX (indirecció amb `[]`, `@@etiqueta`). |
| `--define NOM[,NOM…]` | `-d` | Defineix símbols amb valor `1` (llista separada per comes). |
| `--encoding NOM` | | Codificació per als literals de text (`.ascii` i companyia). Per defecte `utf-8`. |
| `--include RUTES` | `--inc` | Rutes on buscar els fitxers inclosos amb `<...>`, separades per `;`. |
| `--input-file F` | `--input` | Manera alternativa d'indicar el fitxer d'entrada. |
| `--instruction-set S` | `--instr` | Joc d'instruccions: una clau interna (`z80`, `z80alt`) o la ruta d'un fitxer propi. |
| `--listing F` | `-l` | Escriu un listing de l'ensamblatge al fitxer indicat. |
| `--list-encodings` | | Llista les codificacions disponibles i acaba. |
| `--nest-macros` | | Permet macros niades. |
| `--output-file F` | `--output` | Manera alternativa d'indicar el fitxer de sortida. |
| `--symbols F` | `-s` | Escriu les etiquetes i les seves adreces en format `.equ`. Sense argument, `[entrada].sym`. |
| `--verbose[:nivell]` | `-v` | Detall de sortida: `0` quiet, `1` minimal, `2` normal, `3` detailed, `4` diagnostic. |
| `--help` | `-h`, `-?`, `/?` | Ajuda (de moment, incompleta). |

---

## Sintaxi

Els exemples són en z80.

`sasSX` intenta acostar-se a les sintaxis habituals:

```asm
        ld b, 20 + 0x13
    etiqueta1:
        inc c
        djnz etiqueta1
```

És insensible a majúscules i minúscules, i els espais i tabuladors es poden posar
com es vulgui. L'excepció és que per ajuntar dues instruccions en una línia cal
posar `\` on aniria el salt de línia: `ld a, 1 \ add a, b`. Ara bé, `AD D a, b`
no és vàlid, ni `ADDa, b`. Les etiquetes admeten la forma `etiqueta:` (preferida) o
`:etiqueta`.

S'accepten gairebé totes les formes d'una instrucció:

```asm
    cp a, (ix + 10)
    cp a, (10 + ix)
    cp (ix + 10)
    cp (ix)
    cp a, (ix)
```

### Expressions

Allà on cal un nombre, s'hi pot posar una expressió. S'avaluen amb la precedència
d'operadors de C. Operadors disponibles:

```
* / % + - << >> < <= > >= == != & ^ | && ||
```

Els operadors booleans donen `1` si cert i `0` si fals. Els nombres es poden
escriure en decimal, hexadecimal (`0x1F`, `$1F`, `1Fh`), binari (`0b1010`,
`1010b`) i octal (`0o17`), i poden ser negatius. El prefix `%` per a binari
(`%1010`) encara xoca amb l'operador mòdul; useu `0b…` o `…b`.

### Adreçament relatiu

Es poden definir tantes etiquetes anomenades `_` com calgui i referir-s'hi amb
`[-+]*_` per apuntar a les més properes. `+` apunta a la següent, `++` a la del
darrere, i `-` a l'anterior:

```asm
    _: ; A
        jp _   ; apunta a B
        jp -_  ; apunta a A
        jp ++_ ; apunta a C
    _: ; B
        ld a, b
    _: ; C
```

### Etiquetes locals

Una etiqueta amb `.` al davant és local dins de l'última etiqueta global. Així es
poden reaprofitar noms com `bucle`:

```asm
    global1:
        ld a, b
    .local:
        call .local
    global2:
        ld b, a
    .local:
        call .local ; no dona error de nom duplicat
```

Amb `--asmsx`, `@@local` és sinònim de `.local`.

### Macros

```asm
    .macro exemple(foo, bar)
        ld a, foo
        ld b, bar
    .endmacro

    exemple(1, 2) ; passa a: ld a, 1 \ ld b, 2
```

Sense paràmetres també val (`.macro exemple` … `.endmacro`). Les macros niades
estan permeses (`--nest-macros`).

---

## Directives

Les directives comencen per `.` o per `#`.

### Generals

| Directiva | Què fa |
| --- | --- |
| `.ascii "text"` | Insereix el text en la codificació global (**no** ASCII). |
| `.asciiz "text"` | Igual, amb un zero al final. |
| `.asciip "text"` | Igual, precedit de la seva llargada en 8 bits. |
| `.db val, val, …` | Insereix bytes. `.byte` n'és sinònim; sense valor, `0`. |
| `.dw val, val, …` | Insereix paraules (16 bits a z80). `.word` n'és sinònim. |
| `.block mida` | Reserva `mida` bytes a `0`. |
| `.fill mida[, valor]` | Insereix `mida` còpies de `valor` (per defecte `0`). Límit `0xFFFF`. |
| `.ds mida[, valor]` | Sinònim de `.fill`. |
| `.equ clau valor` | Crea un símbol amb el valor de l'expressió. |
| `.define clau valor` | Crea una macro d'una línia. |
| `.undefine clau` | Elimina una definició. |
| `.if expr` / `.ifdef s` / `.ifndef s` | Assemblatge condicional fins a `.endif`. |
| `.else` / `.elif` / `.elseif` | Branca alternativa. |
| `.include "fitxer"` / `.include <fitxer>` | Insereix un fitxer. `"..."` el busca al directori actual; `<...>`, a les rutes de `--include`. |
| `.list` / `.nolist` | Reprèn / atura el listing. |
| `.error "text"` | Atura amb un error. |
| `.echo msg, …` | Trau missatges en temps d'ensamblatge (**a mig fer**). |
| `.end` / `.endfile` | Fi de l'ensamblatge. |
| `.exec ordre [args]` | Executa una ordre externa i insereix la seva sortida estàndard al binari. |
| `.org valor` | Fixa el comptador de programa. No afegeix res a la sortida. |

**`.define` o `.equ`?** `.define` crea una macro: té més sobrecàrrega i s'ha
d'usar amb mesura. `.equ` iguala un nom a un valor i crea un símbol, molt més
barat. Quan es pugui, `.equ`. Un cas on cal `.define` és per a una constant de
text:

```asm
    .define text "Hola, món"
    .asciiz text

    .equ valor 0x1234 - 28
    ld hl, valor
```

### Específiques d'MSX

| Directiva | Què fa |
| --- | --- |
| `.page N` | Situa el codi a la pàgina `N` (0–3) de 16K: `0`→`0x0000`, `1`→`0x4000`, `2`→`0x8000`, `3`→`0xC000`. |
| `.rom` | Mode ROM: la sortida es genera en trams de 8K des de `0x4000`. |
| `.start etiqueta` | Punt d'entrada del cartutx; hi posa la capçalera de 16 bytes (`AB` + punter). |
| `.bios` | Porta les etiquetes de la BIOS (`bios.asm` implícit). |
| `.megarom mapejador` | Capçalera i estructura de megaROM. Defineix ja la subpàgina 0. **A mig fer.** |
| `.subpage N at $ADREÇA` | Subpàgina d'una megaROM en una adreça. |
| `.module nom` / `.endmodule` | Etiquetes amb espai de noms: `nom.etiqueta`. |
| `.phase X` / `.dephase` | Ensambla en una adreça però declara les etiquetes en una altra (codi que després es copia a un altre lloc). **A mig fer.** |
| `.rept N` / `.endr` | Repeteix el bloc `N` vegades. **A mig fer** (no omple els bytes). |

Mapejadors de `.megarom`:

| Mapejador | Subpàgina | Límit | Mida màxima | Notes |
| --- | --- | --- | --- | --- |
| `Konami` | 8K | 32 pàgines | 256 KB | La subpàgina 0 (`0x4000`–`0x5FFF`) és fixa. |
| `KonamiSCC` | 8K | 64 pàgines | 512 KB | Accés al xip de so SCC de Konami. |
| `ASCII8` | 8K | 256 pàgines | 2 MB | |
| `ASCII16` | 16K | 256 pàgines | 4 MB | |

---

## Full de ruta

Reemplaça l'antic `sass/Todo.txt` (juliol de 2016).

### Fet

- Línies amb comentari `;` que donaven error en ensamblar.
- Mode `--asmsx`: taula alternativa i codi generat compatible amb asMSX;
  `@@etiqueta` com a local, també dins d'`.include`.
- `.incbin` amb desplaçament i mida, i el PC s'actualitza amb els bytes afegits.
- `.ds` com a sinònim de `.fill`; límit de `0xFFFF` a `.fill` i `.ds`.
- Tabulador entre `.org` i l'adreça (abans s'ignorava l'`.org`).
- Nombres negatius a `.db` i a les expressions (`X .equ 0-17` ja no dona
  *ValueTruncated*). Corregit del tot a la revifada, amb la conversió
  `double → ulong` passant per `long`.
- `.module` / `.endmodule`.
- `/* */` i `//` tractats en llegir el fitxer, per mantenir els números de línia.
- `--verbose` amb nivells (`0`–`4`).
- Port a .NET 10 (SDK-style); `.incbin` amb `ReadExactly` (lectura parcial).

### A mig fer

- `.megarom`: arreglar la detecció de mapejador i el control de sobreeiximent de
  subpàgina; avisar de les pàgines no definides.
- `.phase` / `.dephase`: reubicar bé les referències absolutes dins del bloc.
- `.rept` / `.endr`: omplir els bytes del bloc (ara queden a `00`).
- `.block`: aplicar-hi el límit de `0xFFFF`.

### Pendent

- Un `--help` de debò.
- `echo` / `print` / `printdec` / `printtext` amb format per cada cas.
- Validar el càlcul d'expressions (`.equ valor 0x1234 - 28`).
- El prefix `%` per a binari (`%1010`), que ara xoca amb l'operador mòdul.
- Taula de símbols en format asMSX (`00h:401Ch ETIQUETA`) per als depuradors de
  BlueMSX i openMSX.
- Substituir els `Console.WriteLine` de diagnòstic per avisos i errors
  estructurats a les entrades del listing.
- Encapsular els camps de configuració de `Assembler` en un tipus `Parameters`.
- No petar amb `IndexOutOfRangeException` davant d'entrades malformades.
- Netejar els avisos de l'analitzador ara que és SDK-style (nullable, `CA…`).
- Implementar `.basic` o treure-la.

---

## Llicència i crèdits

Fork de [*SirCmpwn's Assembler*](https://github.com/KnightOS/sass) (projecte
KnightOS), amb les modificacions per a MSX de Libertium Games. L'upstream es
distribueix sota llicència MIT.

## Errors i suggeriments

Obriu una incidència a
[github.com/Libertium/sass-MSX](https://github.com/Libertium/sass-MSX/issues).
