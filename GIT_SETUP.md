# Git Setup Instructions

## Steps to Push to Existing Repository

### 1. Add Remote Repository

```powershell
git remote add origin <YOUR_REPO_URL>
```

**Examples:**
- GitHub: `git remote add origin https://github.com/username/repo-name.git`
- GitHub SSH: `git remote add origin git@github.com:username/repo-name.git`
- Azure DevOps: `git remote add origin https://dev.azure.com/organization/project/_git/repo-name`
- GitLab: `git remote add origin https://gitlab.com/username/repo-name.git`

### 2. Check What Will Be Committed

```powershell
git status
```

### 3. Add All Files

```powershell
git add .
```

### 4. Commit

```powershell
git commit -m "Initial commit: GuardianAI - Content Moderation Agent with text and image classification"
```

### 5. Push to Repository

**If repository is empty:**
```powershell
git push -u origin main
```

**If repository has content (use force push carefully):**
```powershell
git push -u origin main --force
```

**Or if default branch is `master`:**
```powershell
git push -u origin master
```

## Verify Remote

```powershell
git remote -v
```

## Common Issues

### Issue: "fatal: refusing to merge unrelated histories"
**Solution:**
```powershell
git pull origin main --allow-unrelated-histories
git push -u origin main
```

### Issue: Branch name mismatch
**Check current branch:**
```powershell
git branch
```

**Rename branch if needed:**
```powershell
git branch -M main
```

## Files Excluded (.gitignore)

- `bin/`, `obj/` - Build outputs
- `node_modules/` - Node.js dependencies
- `*.dll`, `*.pdb` - Compiled files
- `.vs/`, `.vscode/` - IDE settings
- `appsettings.Development.json` - Local config (create template if needed)
- `models/` - ML models (optional)
- Database files (`.mdf`, `.ldf`)

## Next Steps After Push

1. Create `appsettings.Development.json.template` if needed
2. Add README with setup instructions
3. Set up CI/CD if needed
4. Add branch protection rules
