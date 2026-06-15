#!/bin/bash

# Git Push Script with Token Authentication
# Reads GitHub token from /home/scott/github_token

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

TOKEN_FILE="/home/scott/github_token"
REPO_DIR="/home/scott/flashback/"

echo -e "${YELLOW}GitHub Push Script${NC}"
echo "================================"

# Check if token file exists
if [ ! -f "$TOKEN_FILE" ]; then
    echo -e "${RED}Error: Token file not found at $TOKEN_FILE${NC}"
    exit 1
fi

# Read token from file (trim whitespace)
GITHUB_TOKEN=$(cat "$TOKEN_FILE" | tr -d '[:space:]')

if [ -z "$GITHUB_TOKEN" ]; then
    echo -e "${RED}Error: Token file is empty${NC}"
    exit 1
fi

echo -e "${GREEN}✓${NC} Token loaded from $TOKEN_FILE"

# Change to repository directory
cd "$REPO_DIR"
echo -e "${GREEN}✓${NC} Changed to repository directory"

# Get current branch
CURRENT_BRANCH=$(git branch --show-current)
echo -e "${GREEN}✓${NC} Current branch: $CURRENT_BRANCH"

# Check for uncommitted changes
if ! git diff-index --quiet HEAD --; then
    echo -e "${YELLOW}Warning: You have uncommitted changes${NC}"
    git status --short
    read -p "Continue with push? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Push cancelled"
        exit 0
    fi
fi

# Push to GitHub
echo ""
echo "Pushing to GitHub..."
echo "Repository: https://github.com/Scomps/flashback.git"
echo "Branch: $CURRENT_BRANCH"
echo ""

if git push https://Scomps:$GITHUB_TOKEN@github.com/Scomps/flashback.git $CURRENT_BRANCH; then
    echo ""
    echo -e "${GREEN}✓ Successfully pushed to GitHub!${NC}"
    echo ""
    echo "View your changes at:"
    echo "https://github.com/Scomps/flashback/tree/$CURRENT_BRANCH"
    exit 0
else
    echo ""
    echo -e "${RED}✗ Push failed${NC}"
    exit 1
fi

# Made with Bob
