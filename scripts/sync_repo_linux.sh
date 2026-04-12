#!/bin/bash
# Flashback Suite - Linux Sync Script
# This script handles cloning/pulling from the private repository.

REPO_URL="git@github.com:SComps/Flashback.git"
REPO_DIR="Flashback"

# Check for git
if ! command -v git &> /dev/null; then
    echo "Error: git is not installed. Please install it first (e.g., sudo apt install git)."
    exit 1
fi

if [ -d "$REPO_DIR/.git" ]; then
    echo "Updating existing Flashback repository..."
    cd "$REPO_DIR"
    git pull
else
    if [ -d "$REPO_DIR" ]; then
        echo "Error: Directory $REPO_DIR exists but is not a git repository."
        exit 1
    fi

    echo "Cloning Flashback repository..."
    git clone "$REPO_URL"
    
    if [ $? -ne 0 ]; then
        echo -e "\n----------------------------------------------------------------"
        echo "CLONE FAILED: Authentication issue detected."
        echo "Since the repo is PRIVATE, you must use SSH Keys or a Token."
        echo ""
        echo "Option A: SSH (Recommended)"
        echo "  1. Ensure your SSH public key (~/.ssh/id_rsa.pub) is added to:"
        echo "     https://github.com/settings/keys"
        echo "  2. Test connection: ssh -T git@github.com"
        echo ""
        echo "Option B: HTTPS with Personal Access Token"
        echo "  Run this manually to clone via HTTPS:"
        echo "  git clone https://YOUR_GITHUB_USERNAME:YOUR_TOKEN@github.com/SComps/Flashback.git"
        echo "----------------------------------------------------------------"
        exit 1
    fi
    cd "$REPO_DIR"
    
    # Make other scripts executable
    if [ -d "scripts" ]; then
        chmod +x scripts/*.sh
        echo "Permissions set for scripts/ directory."
    fi
fi

echo -e "\nSync complete. Flashback is up to date in: $(pwd)"
