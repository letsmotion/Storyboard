#!/bin/bash
echo "========================================"
echo "Timeline Draft Verification"
echo "========================================"
echo ""

PROJECT_DIR="C:/Users/Administrator/AppData/Local/Storyboard/output/projects/6b9ccb0285744a008a5cf96af3756f07"
DRAFT_DIR="$PROJECT_DIR/draft"
LOG_FILE="C:/Users/Administrator/AppData/Local/Storyboard/logs/app-$(date +%Y%m%d).log"

echo "1. Checking project directory..."
if [ -d "$PROJECT_DIR" ]; then
    echo "   ✓ Project directory exists: $PROJECT_DIR"
else
    echo "   ✗ Project directory not found"
    echo "   This is normal for first run - it will be created when you open the project"
fi
echo ""

echo "2. Checking draft directory..."
if [ -d "$DRAFT_DIR" ]; then
    echo "   ✓ Draft directory exists"
    ls -lh "$DRAFT_DIR"
else
    echo "   ✗ Draft directory not found yet"
    echo "   It will be created when you open the project"
fi
echo ""

echo "3. Checking draft files..."
if [ -f "$DRAFT_DIR/draft_content.json" ]; then
    echo "   ✓ draft_content.json exists"
    SIZE=$(stat -c%s "$DRAFT_DIR/draft_content.json" 2>/dev/null || stat -f%z "$DRAFT_DIR/draft_content.json" 2>/dev/null)
    echo "     Size: $SIZE bytes"
fi

if [ -f "$DRAFT_DIR/draft_meta_info.json" ]; then
    echo "   ✓ draft_meta_info.json exists"
fi
echo ""

echo "4. Recent log entries (last 20 lines with '草稿' or 'draft')..."
if [ -f "$LOG_FILE" ]; then
    tail -100 "$LOG_FILE" | grep -iE "(草稿|draft|Timeline)" | tail -20
else
    echo "   No log file found for today"
fi
echo ""

echo "========================================"
echo "Next Steps:"
echo "1. Launch the application"
echo "2. Open project '1' (ID: 6b9ccb0285744a008a5cf96af3756f07)"
echo "3. Run this script again to verify draft files were created"
echo "4. Switch to Timeline view to see the timeline"
echo "========================================"
