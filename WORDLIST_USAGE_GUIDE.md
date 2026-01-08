# Wordlist Usage Guide

## 📝 Šta je Wordlist?

Wordlist je dinamička lista riječi koje agent koristi za detekciju problematičnog sadržaja. Agent kombinuje:
- **Base keywords** (hardcoded u kodu) - osnovne riječi
- **Dynamic wordlist** (iz baze) - riječi koje dodaješ kroz UI

## ✅ Da li je normalno da je wordlist prazan?

**DA, normalno je!** Wordlist je prazan na početku jer:
- Agent već koristi base keywords (hardcoded u kodu)
- Dinamički wordlist je za dodatne riječi koje želiš blokirati
- Možeš dodati riječi kroz UI kada ih vidiš

## 🎯 Kada dodati riječi u wordlist?

Dodaj riječi kada:
1. **Vidiš novi slur ili uvredljivu riječ** koja nije već blokirana
2. **Agent propušta određene riječi** koje želiš blokirati
3. **Imaš specifične riječi** za svoju domenu (npr. brand names, specifični termini)

## 📋 Kategorije

- **toxic**: Uvredljive, toksične riječi (npr. "fuck", "bitch", "idiot")
- **hate**: Riječi koje izražavaju mržnju (npr. "hate", "kill", "die")
- **spam**: Spam fraze (npr. "buy now", "click here", "limited time")
- **offensive**: Uvredljive riječi (npr. "damn", "shit", "hell")
- **slur**: Specifični slurs (dodaj ručno kroz UI)

## 🔧 Kako koristiti

### 1. Dodaj novu riječ:
- Otvori "Wordlist" stranicu
- Unesi riječ (npr. "slur-word")
- Odaberi kategoriju (npr. "slur")
- Klikni "Add Word"

### 2. Edit/Delete/Activate:
- **Edit**: Promijeni riječ ili kategoriju
- **Activate/Deactivate**: Uključi/isključi riječ (ne briše je)
- **Delete**: Obriši riječ potpuno

### 3. Filter:
- Koristi dropdown da filtriraš po kategorijama
- Vidi samo riječi iz određene kategorije

## 💡 Preporuke

1. **Dodaj riječi koje agent propušta** - ako vidiš da agent ne blokira određenu riječ, dodaj je
2. **Koristi kategorije ispravno** - slurs idu u "slur", ali se također koriste za toxic/hate/offensive
3. **Ne dodavaj previše riječi odjednom** - dodaj ih kako ih vidiš u praksi
4. **Deaktiviraj umjesto brisanja** - ako nisi siguran, deaktiviraj riječ umjesto da je brišeš

## ⚠️ Važno

- **Wordlist se primjenjuje odmah** - nema potrebe za retraining
- **Retraining NE dodaje riječi** - retraining trenira ML model, ne mijenja wordlist
- **Base keywords su uvek aktivni** - hardcoded riječi u kodu se uvijek koriste
- **Wordlist je dodatak** - dinamičke riječi se kombinuju sa base keywords

## 🎓 Primjer

**Scenario:**
1. Vidiš komentar sa riječju "slur-word" koji agent nije blokirao
2. Otvoriš Wordlist stranicu
3. Dodaješ "slur-word" u kategoriju "slur"
4. Sada kada agent vidi "slur-word" → automatski blokira

**Ne treba retraining!** Wordlist se primjenjuje odmah.
