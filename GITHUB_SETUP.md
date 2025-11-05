# GitHub & Unity Version Control Setup Guide

This guide covers two options for collaborative Unity development:
1. **GitHub** (Git-based, free, industry standard)
2. **Unity Version Control** (formerly Plastic SCM, Unity's official solution)

---

## Option 1: GitHub Setup (Recommended for Most Projects)

### Prerequisites
- GitHub account (https://github.com)
- Git installed on your machine

### Step 1: Initialize Git Repository

Open Terminal and navigate to your project:
```bash
cd /Users/henrywagner/Documents/598/FinalProject/598_v1
git init
```

### Step 2: Add Files to Git

```bash
# Add all files (respects .gitignore)
git add .

# Create initial commit
git commit -m "Initial commit - VR EditableCube project"
```

### Step 3: Create GitHub Repository

1. Go to https://github.com/new
2. Repository name: `598-vr-project` (or whatever you prefer)
3. **IMPORTANT**: Do NOT initialize with README, .gitignore, or license (we already have them)
4. Click "Create repository"

### Step 4: Connect and Push

GitHub will show you commands. Use these:

```bash
# Connect to GitHub
git remote add origin https://github.com/YOUR-USERNAME/598-vr-project.git

# Push to GitHub
git branch -M main
git push -u origin main
```

### Step 5: Collaborate

**For teammates to clone:**
```bash
git clone https://github.com/YOUR-USERNAME/598-vr-project.git
cd 598-vr-project
```

**Open in Unity Hub:**
- Add → Add project from disk
- Navigate to cloned folder
- Unity will reimport everything automatically

### Daily Workflow

**Pull latest changes:**
```bash
git pull
```

**Make changes, then commit:**
```bash
git add .
git commit -m "Description of what you changed"
git push
```

### Handling Conflicts

**If you get conflicts in .unity or .prefab files:**
1. Use Unity's Smart Merge tool (see below)
2. Or manually resolve in text editor
3. Test in Unity before committing

---

## Option 2: Unity Version Control (Plastic SCM)

### What is Unity Version Control?

- Built specifically for Unity projects
- Better at handling Unity's YAML files
- Visual diff for scenes/prefabs
- Free for up to 3 users
- Integrated into Unity Editor

### Setup Unity Version Control

#### Step 1: Enable in Unity

1. **Window → Version Control**
2. Click **"Get Started"**
3. Choose **"Unity Version Control"** (formerly Plastic SCM)
4. Sign in or create Unity ID

#### Step 2: Create Repository

1. Click **"Create Repository"**
2. Repository name: `598-vr-project`
3. Choose cloud or on-premise server
4. Click **Create**

#### Step 3: Initial Checkin

1. Select all files in the Pending Changes view
2. Add a comment: "Initial commit"
3. Click **"Check in"**

### Daily Workflow with Unity Version Control

**Pull changes:**
- Click **"Update"** button in Version Control window

**Make changes:**
- Unity automatically tracks file changes
- View in **Pending Changes** tab

**Commit changes:**
- Select changed files
- Add comment
- Click **"Check in"**

### Invite Collaborators

1. **Window → Version Control → Settings**
2. Click **"Manage Users"**
3. Invite by email

---

## Comparison: GitHub vs Unity Version Control

| Feature | GitHub | Unity Version Control |
|---------|--------|----------------------|
| **Cost** | Free (unlimited public/private repos) | Free (up to 3 users, 5GB storage) |
| **Integration** | External (Terminal/GitHub Desktop) | Built into Unity Editor |
| **Unity Scenes** | Can cause merge conflicts | Visual scene merging |
| **Learning Curve** | Need to learn Git | Easier for Unity-only work |
| **Industry Use** | Very common | Less common outside Unity |
| **Storage** | Unlimited | 5GB free tier |
| **Best For** | Teams familiar with Git | Unity-only teams, beginners |

---

## Recommended Setup: GitHub with Unity Smart Merge

### Setup Unity Smart Merge (Best of Both Worlds)

This makes Git smarter about merging Unity files!

1. **Edit → Project Settings → Editor**
2. Find **"Version Control"**
3. Set **Mode** to **"Visible Meta Files"**
4. Set **Asset Serialization** to **"Force Text"**

5. **Configure Git merge tool:**

```bash
# On macOS/Linux, edit ~/.gitconfig
git config --global merge.tool unityyamlmerge

git config --global mergetool.unityyamlmerge.cmd '/Applications/Unity/Hub/Editor/YOUR-UNITY-VERSION/Unity.app/Contents/Tools/UnityYAMLMerge merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'

git config --global mergetool.unityyamlmerge.trustExitCode false
```

Now when you have conflicts:
```bash
git mergetool
```

Unity will intelligently merge scene/prefab files!

---

## Quick Start: Which Should You Use?

### Use GitHub if:
- ✅ You or team knows Git
- ✅ Want industry-standard workflow
- ✅ Need integration with CI/CD
- ✅ Working on multiple projects (not just Unity)

### Use Unity Version Control if:
- ✅ Unity is your only tool
- ✅ Team is uncomfortable with Git
- ✅ Want visual scene diff/merge
- ✅ Small team (3 or fewer)

### Use Both? (Advanced)
You can actually use Git for code and Unity VC for assets, but this is complex. Not recommended for beginners.

---

## Important Files Already Created

✅ `.gitignore` - Tells Git which files to ignore (Library, Temp, etc.)
✅ `.gitattributes` - Handles line endings and binary files
✅ `README.md` - Project documentation

You're ready to push to GitHub! Just follow Option 1 steps above.

---

## Common Unity + Git Pitfalls

### ❌ DON'T:
- Commit the Library folder (it's huge and auto-generated)
- Forget to set Asset Serialization to "Force Text"
- Work on the same scene file simultaneously (causes conflicts)
- Force push to main/master branches

### ✅ DO:
- Pull before starting work
- Commit and push frequently (small commits)
- Use descriptive commit messages
- Test before pushing
- Use .gitignore properly

---

## Need Help?

- **Git Issues**: https://docs.github.com/en/get-started
- **Unity VC**: https://unity.com/products/unity-version-control
- **Smart Merge**: https://docs.unity3d.com/Manual/SmartMerge.html

Happy collaborating! 🚀

