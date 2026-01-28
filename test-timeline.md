# Timeline Feature Testing Checklist

## ✅ Pre-Test Setup
- [x] Code fix applied to MainViewModel.cs
- [x] Build successful (0 errors)
- [x] Projects directory created

## 📋 Test Checklist

### Test 1: Draft Auto-Creation
**Steps:**
1. Launch application: `dotnet run`
2. Open project "1" (ID: 6b9ccb0285744a008a5cf96af3756f07)
3. Check log output

**Expected Results:**
```
[INF] 项目数据加载成功: 3 个镜头
[INF] 创建新草稿: {DraftId}
[INF] 草稿已保存到: C:\Users\Administrator\AppData\Local\Storyboard\output\projects\6b9ccb0285744a008a5cf96af3756f07\draft
```

**Verify:**
```bash
bash verify-draft.sh
```

Should show:
- ✓ Draft directory exists
- ✓ draft_content.json exists
- ✓ draft_meta_info.json exists

---

### Test 2: Timeline View
**Steps:**
1. In the application, switch to "Timeline" view
2. Observe the timeline display

**Expected Results:**
- Timeline shows 1 video track
- Track contains 3 video segments
- Segments are arranged sequentially
- Each segment shows duration

---

### Test 3: Draft Content Verification
**Steps:**
```bash
# View draft content
cat "C:/Users/Administrator/AppData/Local/Storyboard/output/projects/6b9ccb0285744a008a5cf96af3756f07/draft/draft_content.json" | jq .
```

**Expected Structure:**
```json
{
  "id": "...",
  "name": "项目名称",
  "duration": 10500000,
  "fps": 30.0,
  "tracks": [
    {
      "type": "video",
      "segments": [
        { "material_id": "...", "target_timerange": {...} },
        { "material_id": "...", "target_timerange": {...} },
        { "material_id": "...", "target_timerange": {...} }
      ]
    }
  ],
  "materials": {
    "videos": [...]
  }
}
```

---

### Test 4: Real-time Preview
**Steps:**
1. In Timeline view, click on a video segment
2. Observe the video player

**Expected Results:**
- Video player loads the segment's video
- Video plays correctly
- Duration matches the segment

---

### Test 5: Auto-Save
**Steps:**
1. Note the current timestamp of draft_content.json
2. Wait 5 seconds
3. Check if file was updated

**Verify:**
```bash
stat "C:/Users/Administrator/AppData/Local/Storyboard/output/projects/6b9ccb0285744a008a5cf96af3756f07/draft/draft_content.json"
```

---

## 🐛 Troubleshooting

### Issue: Draft directory not created
**Check:**
1. Log file: `tail -f C:/Users/Administrator/AppData/Local/Storyboard/logs/app-$(date +%Y%m%d).log`
2. Look for errors related to "草稿" or "draft"

**Common causes:**
- Permission issues
- Project path incorrect
- Message handlers not firing

### Issue: Timeline view is empty
**Check:**
1. Verify draft_content.json has data
2. Check log for "BuildTimelineFromDraft" messages
3. Verify shots have generated videos

### Issue: "无法获取项目信息" still appears
**This means:**
- The fix didn't apply correctly
- Need to rebuild: `dotnet build --no-incremental`

---

## 📊 Success Criteria

All tests pass when:
- ✅ Draft files created automatically
- ✅ Timeline displays correctly
- ✅ JSON files contain valid data
- ✅ Video preview works
- ✅ Auto-save functions
- ✅ No errors in logs

---

## 📞 Next Steps After Testing

If all tests pass:
1. ✅ Timeline feature is fully functional
2. ✅ Ready for production use
3. ✅ Can export to CapCut

If issues found:
1. Check logs for specific errors
2. Run `bash verify-draft.sh` for diagnostics
3. Review the error messages
