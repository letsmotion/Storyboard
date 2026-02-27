  尚未覆盖的 UI 需求：                                                                                                                                                          
                                                                                                                                                                                
  - 当前 UI 中尚未暴露音乐合成、实时对话、Responses/Embeddings 等入口；new-api 虽支持这些 OpenAI 形态，但我们还没实现对应 provider，所以如果未来界面要出现这些按钮，需要另写接口    层。                                                                                                                                                                        
  - 图片模块只接了“生成”，UI 尚未实现 OpenAI 图像的“编辑/变体”功能，若后续要透出，需要对 new-api 的 /images/edits、/images/variations 做补充。                                  
  - 视频界面目前只处理单 prompt 的生成，没有展示 new-api 文档中更复杂的 metadata, input_audio 等字段，UI 也没入口，所以暂不影响；要用这些高级调参，再延伸表单即可。             
                                                                                                                                                                                
  总结：现有的 UI 核心功能（文本、图像、视频生成）都可以直接切换到 new-api，体验与之前自适配 Qwen/火山时一致；若将来 UI 扩展更多 new-api 能力，还需要按需求加 provider 接口和配 
  置表单。      