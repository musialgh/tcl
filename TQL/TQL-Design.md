TQL steht natürlich für *TAS Query Language_. Es handelt sich um eine *Domain Specific Language* (DSL) für die Definition von
- Wettbewerbs-Bewertungs- und Auswerte-Schemata (pro Rundenelement, Runde, Turnier)
- Bildungsregeln für Runden und Folge-Turniere (inkl. Klassifikationsrunden oder "Sieger tanzt mit")
- Regeln für die Ergebnisberichte (**TBD**: TQL-Nutzung sinnvoll?)

**Design-Kriterium**: *Wenn sich sowohl das Skating-System als auch eine Fußball-WM damit modellieren lassen, sind wir für zukünftige Anforderungen aus dem Tanzsport gut aufgestellt.*

# TAS-Interface

TQL ist grundsätzlich eine touringmächtige Programmiersprache, die *alle* erfindbaren Berechnungsaufgaben lösen kann. Das TAS-Interface definiert erst den eigentlich *domänen-spezifischen* Aspekt in Form  der Anbindung von TQL an TAS. Es handlet sich im Kern um zwei Tabellen mit einer vereinbarter Struktur, die *beliebige* Wettbewerbsarten unterstützt.

## Eingabe

- Tabelle **marks** (start, round, round\_element, adjudicator\_group, adjudicator, value DECIMAL)
  - als *start* kann (muss aber nicht) die Startnummer verwendet werden
- Variable **round** enthält den Schlüssel für die aktuelle Runde
  - **TBD**: brauchen wir ggf. alle Runden, oder kann das Interface auf die aktuelle Runde beschränkt werden 
    und auf *round* als Eingabe somit verzichten?

## Ausgabe

- Tabelle **report** (start, round, round_element, group TEXT, subgroup TEXT, subsubgroup TEXT, key TEXT, value DECIMAL, reason TEXT)
  - enthält das Endergebnis
  - enthält alle Daten, aus denen Report-Generatoren Auswertungs-Berichte erstellen sollen
  - *reason* dient optional zur Herstellung einer Traceability für den entsprechenden Wert (bzgl. Regeln und/oder verwendeter Eingabe-Daten)
- *report* enthält mindestens
  - einen ansonsten ungruppierten key "*place-min*" pro *start* und *round*, und
  - einen ansonsten ungruppierten key "*place-max*" pro *start* und *round*,
  - um das Endergebnis in Form eines Platz-von- bis Platz-bis-Bereichs darzustellen

## Ablauf

  - TAS stellt die Eingabe-Objekte im globalen Scope zur Verfügung
  - TAS startet das TQL-Skript, das in der Konfiguration der Wettbewerbsart und den aktuellen Rundentyp vorgesehen ist:
    - Aufruf als globaler Code, also noch keine benutzerdefineirte Funktion ausgewählt
    - analog zu EXECUTE
  - Das aufgerufe Skript kann:
    - Funktionsbibliotheken mit IMPORT einbeziehen, und
    - benutzerdefinierte Funktionen aufrufen, die dort oder im Skript selbst definiert sind
  
# Sprachdefinition TQL

Hier eine semi-formale Sprachdefinition von TQL,
- als Dokumentation und
- zur Nutzung in AI-Prompts.

## Sprach-Features und Syntax

**directive**:
- `IMPORT <file-path>`
  - macht alle in der genannten TQL-Datei definierten Funktionen sichtbar für nachfolgenden Code
    - bis zum Ende des Funktionsrumpfs, in dem IMPORT steht, oder
    - für den Rest der Datei, falls IMPORT außerhalb aller Funktionsrümpfe steht
  - es wird empfohlen, `IMPORT` nur im *externen Code* am Beginn einer TQL-Datei zu verwenden
- `EXECUTE <file-path>`
  - bricht die Ausführung der aktuellen TQL-Datei ab und
  - setzt die Ausführung mit dem Top-Level-Code in der TQL-Datei *file-path* fort
  - alle globalen Variablen-Bindungen bleiben erhalten
- `LOAD TABLE <table-name> FROM <csv-path>`
  - lädt eine Tabelle aus einer CSV-Datei und macht sie als *table-name* immer *global* sichtbar
  - Dateiformat siehe `SAVE-TABLE`
- `SAVE TABLE <table-name> INTO <csv-path>`
  - sichert eine einzelne Tabelle in eine CSV-Datei
  - Ausgabe-Format:
    - Trennzeichen `;`
    - Strings (auch Spalten-Namen) sind in `" "` eingeschlossen
    - zum String gehörende `"` sind als `\"` escaped
    - erste Zeile enthält die Spaltennamen
    - alle folgenden Zeilen enthalten jeweils eine Daten-Zeile
- `FUNCTION <function-name>({<param-name>[,]}*{[<param-name> = <default-value>][,]}*):`
  - definiert eine Benutzer-Funktion
  - Basis-Typen (DECIMAL, STRING, NULL) werden als Wert übergeben
  - alle Tabellen-Objekte werden als Referenz übergeben
    - eine Übergabe als Wert kann die Funktion simulieren, indem der Parameter mit LET in eine neue Variable kopiert wird
- `CALL <function-name>({[param-name> = ]<param-value>[,]}*)`
  - ruft benutzerdefinierte oder eingebaute Funktion *function-name* auf
  - Rückgabewert wird ignoriert
- `RETURN [<expression>]`
  - optional für Funktionen ohne Rückgabewert
  - ohne Rückgabewert liefert die Funktion NULL an den Aufrufer
- `ABORT <error-message>`
  - bricht den TQL-Interpreter wegen eines internen/unbehebbaren Fehlers ab
- `LET <identifier> = <expression>`
- `FOR <identifier> IN <limit>..<limit> {[ASC]|DESC}:`
- `FOR <identifier> IN <table>:`
- `FOR <identifier> IN <table>.<column>:`
- `FOR <identifier> IN (<value-list>):`
- `WHILE <condition>:`
- `BREAK`
- `CONTINUE`
- `IF <condition>:`
- `ELSE IF <condition>:`
- `ELSE:`
- `UPDATE <target-table> FROM <source-table> [USING {<source-column> [AS <target-column>][,]}+]`
  - nur Änderung von Werten
  - Wert der ersten Spalte von *source-table* wählt die zu ändernden Zeilen über die erste Spalte der *target-table*
  - eine Quell-Zeile ohne Match in der Ziel-Tabelle führt zum Abbruch
  - wenn es mehr als einen Match pro Quell-Zeile gibt, werden *alle* davon betroffenen Ziel-Zeilen geändert
- `CONCAT TO <target-table> FROM <source-table> [USING {<source-column> [AS <target-column>][,]}+]`
  - nur neue Spalten
  - Wert der ersten Spalte von *source-table* wählt die zu ändernden Zeilen über die erste Spalte der *target-table*
  - eine Quell-Zeile ohne Match in der Ziel-Tabelle führt zum Abbruch
  - wenn es mehr als einen Match pro Quell-Zeile gibt, werden *alle* davon betroffenen Ziel-Zeilen geändert
  - Hinweis: mit CONCAT TO können die meisten verbleibenden Use-Cases von JOIN in Standard-SQL gelöst werden
- `CREATE TABLE <table-name> [IF NOT EXISTS]`
- `DROP TABLE <table-name> [IF NOT EXISTS]`
- `ADD COLUMN <column-name> {GUID|{DECIMAL|TEXT} [DEFAULT <value>]} TO TABLE <table> [IF NOT EXISTS]`
  - Spalten vom Typ GUID in einer neuen Zeile werden automatisch mit einer zufälligen ID versehen

**table-expression**:
- `SELECT {*|<select-expression [AS <column-name>][,]}+ FROM <table>`  
  `[WHERE <where-condition>]`  
  `[GROUP BY <column-list> {[ASC]|DESC}]`  
  `[ORDER BY <column-list> {[ASC]|DESC}]`  
  `[LIMIT <numeric-expression> [ON DISTINCT <column-list>]]`
  - liefert das Ergebnis als Tabellen-Objekt zurück
  - LIMIT … ON DISTINCT zählt die Blöcke mit aufeinanderfolgendem gleichem Wert anstelle von allen Zeilen
    - muss i.d.R. zusammen mit ORDER BY verwendet werden
- `UPDATE <target-table> [WHERE <where-condition>] SET <source-row> [USING {<source-column> [AS <target-column>][,]}+]`
  - nur Änderung von Werten
  - *source-row* muss ein Tabellenausdruck mit genau einer Zeile sein
  - wenn WHERE fehlt, werden alle Zeilen der Ziel-Tabelle geändert
  - liefert die erste Spalte der Ziel-Tabelle zurück, reduziert auf alle von WHERE selektierten Zeilen 
    (egal, ob die neuen Spaltenwerte sich von den alten unterscheiden)
  - darf auch als Direktive genutzt werden
- `INSERT INTO <target-table> VALUES <source-table> [USING {<source-column> [AS <target-column>][,]}+]`
  - nur neue Zeilen
  - liefert eine Tabellenspalte mit der Spalte der Ziel-Tabelle für alle neuen Zeilen zurück
  - darf auch als Direktive genutzt werden
- `ROW({[<column-name> = ]<expression>[,]}+)`
  - liefert eine Tabellenzeile mit den aufgeführten Spaltenname-Wert-Paaren zurück
  - wenn *expression* ein einzelner Bezeichner ist und dem Spaltennamen entspricht, kann *column-name* entfallen
  - Datentypen werden automatisch übernommen
  - DEFAULT für alle Spalten außer vom Typ GUID ist NULL
  - Hinweis: insbesondere nutzbar als *source-row* in UPDATE … SET
- `<table-expression>\[<index>\]`
  - liefert die indizierte Tabellen-Zeile
  - Index 0 liefert die Spaltennamen
  - Index-Bereich für die Daten-Zeilen ist 1..n
- `<table-expression>.<column-name>`
  - liefert die ganze selektierte Spalte

**expression**:
- `<table-expression>`
  - wenn die resultierende Tabelle genau eine Spalte und eine Spalte enthält, wird das Resultat als ein skalarer Wert behandelt
  - dies ist besonders zu beachten, wenn das Resultat später als Funktionsparameter verwendet wird
- `NULL`
- `FALSE`
  - equal to 0
- `TRUE`
  - equal to 1
- `<numeric-expression>`
- `<string-pattern>`
  - in `" "` eingeschlossen
  - Muster der Form `{<expression>}` werden durch den Wert des Ausdrucks ersetzt
  - geschweifte Klammern und doppelte Anführungszeichen müssen escaped werden: `\{`, `\}`, `\"`
  - aufeinanderfolgende *string-pattern*'s werden automatisch konkateniert
    - für Verteilung auf mehrere Code-Zeilen
- `<string>`
  - in `' '` eingeschlossen
  - einfache Anführungszeichen müssen escaped werden: `\'`
  - aufeinanderfolgende *string*'s werden automatisch konkateniert
    - für Verteilung auf mehrere Code-Zeilen
- unäre Operatoren: `NOT`, `-`, `+`, `<value> IS [NOT] NULL`
- binäre Operatoren: `=`, `<>`, `<`, `<=`, `>`, `>=`, `+`, `-`, `*`, `/`, `%`, `AND`, `OR`, `XOR`
- `(<expression>)`
- `<function-name>({[param-name> = ]<param-value>[,]}*)`
  - wertet benutzerdefinierte oder eingebaute Funktion *function-name* aus

**select-expression** (zusätzliche Terme):
- `<column-name>`
- `<group-function>`

**where-condition** (zusätzliche Terme):
- `<column-name> IN <column-expression>`
  - *column-expression* muss ein Tabellen-Ausdruck mit genau einer Spalte sein
    - wird *nicht* im Kontext der im SELECT verwendeten Tabelle ausgewertet
    - Spaltenname wird nicht beachtet
- `VALUES MATCH <source-row>`
  - liefert `TRUE`, wenn alle Spalten-Werte in *source-row* identisch sind mit den gleichnamigen Spalten 
    der im SELECT verwendeten Tabelle
- `<select-expression>`

**group-function**:
- `SUM(<select-expression>)`
- `COUNT({*|<select-expression>})`
  - liefert mit `*` die Gesamtzahl aller Zeilen, die ggf. von WHERE ausgewählt wurden
  - sonst werden alle Werte des Ausdrucks gezählt, die nicht 0/''/NULL sind
- `MIN(<select-expression>)`
- `MAX(<select-expression>)`
- `AVG(<select-expression>)`
- `MEDIAN(<select-expression>)`

**builtin\_functions**:
- `IF(<condition>,<expression-on-true>,<expression-on-false>)`
- `ROWS(<table-expression>)`
  - liefert die Anzahl echter Daten-Zeilen in *table-expression*
  - äquivalent zu: `SELECT COUNT(*) FROM <table-expression>`
  - Ergebnis ist gleichzeitig der größte zulässige Index-Wert für *table-expression[i]*
  - `GUID()`
    - erzeugt eine zufällige Id
    - kann zur Anlage eines eindeutigen Schlüssels in einer neuen Tabellenzeile dienen

## Lexikalische Struktur

- Blöcke für Kontrollstrukturen (FUNCTION, FOR, WHEN, WHILE) werden durch Einrückung definiert (analog Python)
- jede Direktive endet mit einem Semikolon
- jede Direktive kann sich über mehrere Zeilen erstrecken, sofern die Einrückung am Beginn der Direktive
  von keiner der Fortsetzungszeilen unterschritten wird
- Text zwischen `#` und dem Zeilenende ist ein Kommentar
- Tabellen- und Spalten-Namen können werden grundsätzlich ohne Anführungszeichen geschrieben, sie fungieren lexikalisch wie Bezeichner

## Scopes und Ausführung

- TQL-Code außerhalb aller Funktionsrümpfe heißt *globaler Code* und wird ausgeführt
  - beim Interpreter-Start für die aktuelle Datei, oder
  - wenn die aktuelle Datei mit `EXECUTE` aufgerufen wurde
- alle in *globalen Code* mit `LET` definierten Variablen sind nach ihrer Definition sichtbar (**globaler Scope**)
  - für *globalen Code* unterhalb der Definition 
  - für alle Funktionen nach der Definition aufgerufenen Funktionen
    - inklusive solcher, die mit IMPORT importiert wurden
- in Funktionsrümpfen mit `LET` definierte Variablen heißen *lokale Variablen* und sind sichtbar (**lokaler Scope**)
  - im definierenden Funktionsrumpf unterhalb der Definition

## Tabellen und Indizierung

- TQL arbeitet nur auf internen Tabellen-Objekten:
  - Die Anbindung an die Backend-Datenbank erfolgt *nur* vor und *nach* der Ausführung ein oder mehrerer TQL-Skripte, nicht *während* dieser Ausführung.
  - Dadurch wird die Backend-Datenbank nicht inkonsistent, wenn TQL durch einen Fehler abbricht (implizite **Transaktions-Semantik**).
  - Die internen Tabellen können erzeugt werden
    - von der Umgebung vor dem Skript-Aufruf, und/oder
    - vom TQL-Skript via *table-expression*.

## Implementierungsvorschlag für Tabellen-Objekte

```ts
Table = (
    column_list  :        SortedDict<column_name : String,   column_index : Int                   > ; 
    indices      : Vector<SortedDict<cell_value  : TqlValue, row_indices  : Set<Int>>             > ;
    sort_order   :        SortedDict<view_index  : Int,      row_index    : Int                   > ;
    rows         :        SortedDict<row_index   : Int,      row          : Vector<TqlValue>      > ;
    defaults     :        Vector<TqlValue>                                                          ;
)
```

### Rationale

- *column\_list* erlaubt schnellen Zugriff auf Spalten per Name
  - Enumeration nach Spalten-Index wird kaum benötigt (nur Debugging + Ausgabe)
- Zugriffe auf Zeilen über Daten nutzen *indices*
  - jeder Datenwert liefert eine Teilmenge *row_indices* physischer Zeilen
  - unterstützt schnelle Schnittmengenbildung bei Mehrfach-Filterung (z.B. WHERE-Bedingungen)
- die optionale Sortierung von Zeilen unabhängig von den Indices wird separat in *sort\_order* abgelegt
  - für SELECT … ORDER BY technisch erforderlich
  - kann leer sein (default) und wird dann als 1:1-Mapping interpretiert
- Zeilen sind als Vector modelliert:
  - Spalten werden kaum jemals zugefügt => höherer Aufwand ok
  - Vektoren können ggf. mit Reserve-Kapazität angelegt werden, da sie vermutlich nur Referenzen auf *TqlValue*-Objekte enthalten
  - Redundanz Schlüssel zu Daten via Referenzen und Reference Counting addressierbar
- Ablage der DEFAULT-Werte in *defaults* ist kompatibel zum Ablageformat der Zeilen => Initialisierung per Zuweisung möglich
