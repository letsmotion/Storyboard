# 🧪 Timeline Feature - Quick Test Guide

## Step 1: Launch Application

```bash
cd /d/project/分镜大师
dotnet run
```

## Step 2: Open Project

In the application:
1. Click "Open Project" or select existing project
2. Choose project "1" (ID: 6b9ccb0285744a008a5cf96af3756f07)
3. Wait for project to load

## Step 3: Watch for Log Messages

You should see in the console:
```
[INF] 项目数据加载成功: 3 个镜头
[INF] 创建新草稿: {some-guid}
[INF] 草稿已保存到: C:\Users\Administrator\AppData\Local\Storyboard\output\projects\6b9ccb0285744a008a5cf96af3756f07\draft
```

**If you see "无法获取项目信息，跳过草稿加载"** - the fix didn't work, report back.

## Step 4: Verify Draft Files

Open a new terminal and run:
```bash
bash verify-draft.sh
```

Expected output:
```
✓ Project directory exists
✓ Draft directory exists
✓ draft_content.json exists
✓ draft_meta_info.json exists
```

## Step 5: Check Timeline View

In the application:
1. Switch to "Timeline" view (if available)
2. You should see:
   - 1 video track
   - 3 video segments arranged sequentially
   - Each segment showing its duration

## Step 6: Test Video Preview

1. Click on any video segment in the timeline
2. The video player should automatically load and play that segment

---

## ✅ Success Indicators

- Draft files created in correct location
- No error messages in logs
- Timeline displays all segments
- Video preview works

## ❌ If Something Goes Wrong

1. Copy the error message from logs
2. Run: `tail -50 C:/Users/Administrator/AppData/Local/Storyboard/logs/app-$(date +%Y%m%d).log`
3. Share the output

---

## 📊 After Testing

Report back with:
- ✅ or ❌ for each step
- Any error messages
- Screenshots if helpful
