# Kako popraviti Wordlist Database Problem

## Problem
Dobivaš 500 grešku jer tabela `BlockedWords` ne postoji u bazi. Baza je kreirana prije nego što je tabela dodana.

## Rješenje 1: Obriši bazu i kreiraj ponovo (NAJJEDNOSTAVNIJE)

1. Zaustavi backend aplikaciju (Ctrl+C)
2. Obriši bazu:
   ```powershell
   # U SQL Server Management Studio ili kroz command line:
   # DROP DATABASE ContentModerationDb;
   
   # Ili jednostavno obriši fajl ako koristiš LocalDB:
   # Lokacija: C:\Users\HOME\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\
   ```
3. Restart backend - baza će se automatski kreirati sa svim tabelama

## Rješenje 2: Kreiraj tabelu ručno (BRZO)

1. Otvori SQL Server Management Studio ili sqlcmd
2. Poveži se na LocalDB: `(localdb)\mssqllocaldb`
3. Izaberi bazu: `USE ContentModerationDb;`
4. Kreiraj tabelu:
   ```sql
   CREATE TABLE [BlockedWords] (
       [Id] uniqueidentifier NOT NULL PRIMARY KEY,
       [Word] nvarchar(100) NOT NULL,
       [Category] nvarchar(50) NOT NULL,
       [CreatedAt] datetime2 NOT NULL,
       [UpdatedAt] datetime2 NULL,
       [IsActive] bit NOT NULL DEFAULT 1
   );
   
   CREATE INDEX [IX_BlockedWords_Word] ON [BlockedWords] ([Word]);
   CREATE INDEX [IX_BlockedWords_Category] ON [BlockedWords] ([Category]);
   CREATE INDEX [IX_BlockedWords_IsActive] ON [BlockedWords] ([IsActive]);
   ```

## Rješenje 3: Kroz API (AUTOMATSKI)

1. Restart backend (već sam dodao `EnsureCreatedAsync()` koji bi trebao kreirati tabelu)
2. Ako i dalje ne radi, pozovi:
   ```
   POST /api/database/ensure-created
   ```
   Ovo će kreirati sve tabele koje nedostaju.

## Provjera

Nakon bilo kojeg rješenja, provjeri:
```
GET /api/wordlist
```
Trebao bi vratiti prazan array `[]` umjesto 500 greške.
