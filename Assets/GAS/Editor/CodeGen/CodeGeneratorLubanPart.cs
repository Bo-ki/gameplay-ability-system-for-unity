using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GAS.General;

namespace GAS.Editor
{
    public static class CodeGeneratorLubanPart
    {
        // C# 运行时类型 FullName → Luban cfg 类型转换模板（{0} 为字段访问表达式）
        private static readonly Dictionary<string, string> LubanTypeConversionMap = new()
        {
            ["UnityEngine.Vector3"] = "new UnityEngine.Vector3({0}.X, {0}.Y, {0}.Z)",
            ["UnityEngine.Vector2"] = "new UnityEngine.Vector2({0}.X, {0}.Y)",
            ["UnityEngine.Vector4"] = "new UnityEngine.Vector4({0}.X, {0}.Y, {0}.Z, {0}.W)",
        };

        /// <summary>
        /// 将字段名首字母大写，匹配 Luban 的 format_property_name PascalCase 规则
        /// </summary>
        private static string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static void WriteFieldAssignment(
            IndentedWriter writer,
            string paramVar,
            string setter, // BeanFieldAttribute.Setter
            string dataAccessExpr,
            Type fieldType)
        {
            var typeName = fieldType.FullName ?? fieldType.Name;
            if (LubanTypeConversionMap.TryGetValue(typeName, out var template))
            {
                var converted = string.Format(template, dataAccessExpr);
                writer.WriteLine($"{paramVar}?.{setter}({converted});");
            }
            else
            {
                writer.WriteLine($"{paramVar}?.{setter}({dataAccessExpr});");
            }
        }

        /// <summary>
        /// 生成多态 Bean 的拆解代码（通用逻辑）
        /// 包括: 设置 TypeName、创建 Param 实例、switch-case 逐子类赋值、设置 Param
        /// </summary>
        private static void WritePolymorphicFieldAssignment(
            IndentedWriter writer,
            string paramVar, // 宿主变量名, e.g. "tp"
            string dataAccessExpr, // 数据访问路径, e.g. "taskData.Param.CueLogic"
            EXEditorHelper.BeanPolymorphicFieldInfo polyInfo,
            IEnumerable<Type> subtypes, // 多态子类列表
            Func<string, Type> getParamTypeByName) // subtypeName → runtime XParam type
        {
            writer.WriteLine($"// [BeanPolymorphicField] {ToPascalCase(polyInfo.BeanFieldName)}");
            writer.WriteLine($"var polyBean = {dataAccessExpr};");
            writer.WriteLine($"{paramVar}?.{polyInfo.TypeSetter}(polyBean.GetType().Name);");
            writer.WriteLine($"var resolvedParamType = {polyInfo.ParamTypeResolver}(polyBean.GetType().Name);");
            writer.WriteLine("var resolvedParam = Activator.CreateInstance(resolvedParamType) as XParam;");
            writer.WriteLine("if (resolvedParam != null)");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("switch (polyBean)");
                writer.WriteLine("{");
                writer.Indent++;
                {
                    foreach (var subtype in subtypes)
                    {
                        var subtypeName = subtype.Name;
                        var runtimeParamType = getParamTypeByName(subtypeName);

                        // 检查 cfg 侧是否有 Param 成员
                        var tType = ReflectionHelper.GetMemberType($"cfg.{subtypeName}", "Param");
                        if (tType == null) continue;

                        writer.WriteLine($"case cfg.{subtypeName} pData:");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine($"var rp = resolvedParam as {runtimeParamType.FullName};");

                        var beanFields = EXEditorHelper.GetBeanFields(runtimeParamType);
                        foreach (var bf in beanFields)
                            WriteFieldAssignment(writer, "rp", bf.Setter, $"pData.Param.{ToPascalCase(bf.Name)}", bf.MemberType);

                        writer.WriteLine("resolvedParam = rp;");
                        writer.WriteLine("break;");
                        writer.Indent--;
                        writer.WriteLine("}");
                    }

                    writer.WriteLine("default:");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine(
                        $"Debug.LogError($\"[XLuban] Unknown {ToPascalCase(polyInfo.BeanFieldName)} type: {{polyBean.GetType().Name}}\");");
                    writer.WriteLine("break;");
                    writer.Indent--;
                    writer.WriteLine("}");
                }
                writer.Indent--;
                writer.WriteLine("}");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine($"{paramVar}?.{polyInfo.ParamSetter}(resolvedParam);");
        }


        private static (IEnumerable<Type> subtypes, Func<string, Type> getParamType)
            GetPolymorphicHelperInfo(string helperCategory)
        {
            switch (helperCategory)
            {
                case "Cue":
                    return (
                        EditorCueHelper.GetCachedCueTypes(),
                        name => EditorCueHelper.CueToCueParamTypeMap()[name]
                    );
                case "TargetCatcher":
                    return (
                        EditorTargetCatcherHelper.GetCachedTargetCatcherTypes(),
                        name => EditorTargetCatcherHelper.CatcherToParamTypeMap()[name]
                    );
                default:
                    throw new ArgumentException($"Unknown HelperCategory: {helperCategory}");
            }
        }

        private static void WriteTimelineAbilityConfigMethods(IndentedWriter writer)
        {
            var lines = new[]
            {
                "public static XParamTimeline GetTimelineAbilityConfig(int id)",
                "{",
                "    var data = Tables.TbtimelineAbility.GetOrDefault(id);",
                "    if (data == null)",
                "    {",
                "        Debug.LogError($\"TimelineAbility_ID:{id}  不存在.\");",
                "        return null;",
                "    }",
                "",
                "    var tracks = new List<Track>();",
                "    if (data.Tracks != null)",
                "    {",
                "        for (var i = 0; i < data.Tracks.Length; i++)",
                "            tracks.Add(ConvertTimelineTrack(data.Tracks[i]));",
                "    }",
                "",
                "    return new XParamTimeline(data.ID, data.Name, data.LifeTime, data.ManualEndAbility, tracks);",
                "}",
                "",
                "private static Track ConvertTimelineTrack(cfg.Track data)",
                "{",
                "    var track = new Track",
                "    {",
                "        Name = data.Name,",
                "    };",
                "",
                "    if (data.ActionClips != null)",
                "    {",
                "        for (var i = 0; i < data.ActionClips.Length; i++)",
                "            track.ActionClips.Add(ConvertTimelineActionClip(data.ActionClips[i]));",
                "    }",
                "",
                "    return track;",
                "}",
                "",
                "private static TimelineActionClipData ConvertTimelineActionClip(cfg.TimelineActionClip data)",
                "{",
                "    return new TimelineActionClipData",
                "    {",
                "        Name = data.Name,",
                "        StartTime = data.StartTime,",
                "        EndTime = data.EndTime,",
                "        ActionType = data.Action?.GetType().Name ?? string.Empty,",
                "        Parameter = ConvertTimelineActionParameter(data.Action),",
                "    };",
                "}",
                "",
                "private static XParam ConvertTimelineActionParameter(cfg.TimelineActionParameterBase action)",
                "{",
                "    return action switch",
                "    {",
                "        cfg.ApplyEffects applyEffects => ConvertApplyEffectsParam(applyEffects.Param),",
                "        cfg.PlayCue playCue => ConvertCueParam(playCue.Param),",
                "        cfg.PlayCuePreset playCuePreset => new XParamCueList(playCuePreset.Param?.IDs ?? Array.Empty<int>()),",
                "        _ => null,",
                "    };",
                "}",
                "",
                "private static XParamCue ConvertCueParam(cfg.XParamCue data)",
                "{",
                "    if (data?.CueLogic == null)",
                "        return null;",
                "",
                "    var cueLogic = data.CueLogic;",
                "    return new XParamCue(",
                "        cueLogic.GetType().Name,",
                "        ConvertCueLogicParameter(cueLogic),",
                "        data.RequiredTags ?? Array.Empty<int>(),",
                "        data.ImmunityTags ?? Array.Empty<int>());",
                "}",
                "",
                "private static XParamApplyEffects ConvertApplyEffectsParam(cfg.XParamApplyEffects data)",
                "{",
                "    if (data == null)",
                "        return null;",
                "",
                "    var param = new XParamApplyEffects();",
                "    param.SetIDs(data.IDs ?? Array.Empty<int>());",
                "",
                "    if (data.TargetCatcher != null)",
                "    {",
                "        param.SetCatcherType(data.TargetCatcher.GetType().Name);",
                "        param.SetParam(ConvertTargetCatcherParam(data.TargetCatcher));",
                "    }",
                "",
                "    return param;",
                "}",
                "",
                "private static XParam ConvertTargetCatcherParam(cfg.TargetCatcherBase catcher)",
                "{",
                "    switch (catcher)",
                "    {",
                "        case cfg.CatchSelf:",
                "        case cfg.CatchTarget:",
                "            return new XParamNone();",
                "        case cfg.CatchAreaBox3D area:",
                "        {",
                "            var param = new XParamCatchAreaBox3D();",
                "            if (area.Param == null)",
                "                return param;",
                "",
                "            param.SetIsWorldSpace(area.Param.IsWorldSpace);",
                "            param.SetOffset(new UnityEngine.Vector3(area.Param.Offset.X, area.Param.Offset.Y, area.Param.Offset.Z));",
                "            param.SetSize(new UnityEngine.Vector3(area.Param.Size.X, area.Param.Size.Y, area.Param.Size.Z));",
                "            param.SetRotation(new UnityEngine.Vector3(area.Param.Rotation.X, area.Param.Rotation.Y, area.Param.Rotation.Z));",
                "            param.SetLayer(area.Param.Layer);",
                "            return param;",
                "        }",
                "        default:",
                "            return null;",
                "    }",
                "}",
            };

            foreach (var line in lines)
                writer.WriteLine(line);

            writer.WriteLine("");
            WriteCueLogicParameterConverter(writer);
        }

        private static void WriteCueLogicParameterConverter(IndentedWriter writer)
        {
            writer.WriteLine("private static XParam ConvertCueLogicParameter(cfg.GameplayCueBase cueLogic)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (cueLogic == null)");
            writer.WriteLine("    return null;");
            writer.WriteLine("");
            writer.WriteLine("switch (cueLogic)");
            writer.WriteLine("{");
            writer.Indent++;

            var allCue = EditorCueHelper.GetCachedCueTypes();
            var cueTypes = allCue as Type[] ?? allCue.ToArray();
            foreach (var cueType in cueTypes)
            {
                var cueName = cueType.Name;
                var cueParamType = EditorCueHelper.CueToCueParamTypeMap()[cueName];
                Type tType = ReflectionHelper.GetMemberType($"cfg.{cueName}", "Param");
                if (tType == null) continue;

                writer.WriteLine($"case cfg.{cueName} cData:");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"var param = new {cueParamType.FullName}();");

                var beanFields = EXEditorHelper.GetBeanFields(cueParamType);
                foreach (var bf in beanFields)
                    WriteFieldAssignment(writer, "param", bf.Setter, $"cData.Param.{ToPascalCase(bf.Name)}", bf.MemberType);

                var polyFields = EXEditorHelper.GetBeanPolymorphicFields(cueParamType);
                foreach (var pf in polyFields)
                {
                    var (subtypes, getParamType) = GetPolymorphicHelperInfo(pf.HelperCategory);
                    WritePolymorphicFieldAssignment(writer, "param", $"cData.Param.{pf.BeanFieldName}", pf, subtypes, getParamType);
                }

                writer.WriteLine("return param;");
                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.WriteLine("default:");
            writer.Indent++;
            writer.WriteLine("return null;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        public static void GenerateLubanExtension()
        {
            var setting = GASSettingAsset.LoadOrCreate();
            var filePath = setting.PathOfCodeLubanExtesion;
            using var writer = new IndentedWriter(new StreamWriter(filePath));
            writer.WriteLine("///////////////////////////////////");
            writer.WriteLine("//// This is a generated file. ////");
            writer.WriteLine("////     Do not modify it.     ////");
            writer.WriteLine("///////////////////////////////////");

            writer.WriteLine("");

            writer.WriteLine("using System;");
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using cfg;");
            writer.WriteLine("using SimpleJSON;");
            writer.WriteLine("using System.IO;");
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("using System.Linq;");

            writer.WriteLine("");

            writer.WriteLine("namespace GAS.Runtime");
            writer.WriteLine("{");
            writer.Indent++;
            {
                writer.WriteLine("public static class XLuban");
                writer.WriteLine("{");
                writer.Indent++;
                {
                    writer.WriteLine(
                        $"public const string GAME_CONF_DIR = \"{GASSettingAsset.Instance.TableOutpuPath}\";");
                    writer.WriteLine("private static Tables _tables;");
                    writer.WriteLine("public static Tables Tables");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        writer.WriteLine("get");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("if (_tables != null) return _tables;");
                        writer.WriteLine("Debug.LogError(\"XLuban.Tables 未初始化!\");");
                        writer.WriteLine("return null;");
                        writer.Indent--;
                        writer.WriteLine("}");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine("");

                    writer.WriteLine("public static void LoadTables(Func<string, JSONNode> loader)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("if (_tables != null) return;");
                    writer.WriteLine(
                        "_tables = new Tables(loader);");
                    writer.Indent--;
                    writer.WriteLine("}");

                    writer.WriteLine("");

                    writer.WriteLine("public static void LoadTablesForEditor()");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine(
                        "_tables = new Tables(file => JSON.Parse(File.ReadAllText($\"{GAME_CONF_DIR}/{file}.json\")));");
                    writer.Indent--;
                    writer.WriteLine("}");

                    writer.WriteLine("");

                    writer.WriteLine("public static void Init(Func<string, JSONNode> loader)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("LoadTables(loader);");
                    writer.WriteLine("GameplayEffectConfigRegistry.RegisterGetConfigByIDFunc(GetGameplayEffectConfig);");
                    writer.WriteLine("GameplayCueConfigRegistry.RegisterGetConfigByIDFunc(GetGameplayCueConfig);");
                    writer.WriteLine("AbilityConfigRegistry.RegisterGetConfigByIDFunc(GetAbilityConfig);");
                    writer.WriteLine("WarmupConfigRegistryGraph();");
                    writer.Indent--;
                    writer.WriteLine("}");

                    writer.WriteLine("");

                    writer.WriteLine("public static ConfigRegistryGraphWarmupResult WarmupConfigRegistryGraph(bool clearDiagnostics = true)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("if (_tables == null)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("Debug.LogError(\"XLuban.Tables 未初始化!\");");
                    writer.WriteLine("return ConfigRegistryGraphWarmupResult.Empty;");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine("");
                    writer.WriteLine("return ConfigRegistryGraphValidator.Warmup(");
                    writer.Indent++;
                    writer.WriteLine("Tables.Tbability.DataList.Select(data => data.ID),");
                    writer.WriteLine("Tables.TbgameplayEffect.DataList.Select(data => data.ID),");
                    writer.WriteLine("clearDiagnostics);");
                    writer.Indent--;
                    writer.Indent--;
                    writer.WriteLine("}");

                    writer.WriteLine("");

                    #region ASC

                    writer.WriteLine("public static AbilitySystemConfig GetAscConfig(int id)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        writer.WriteLine("var data = Tables.Tbasc.Get(id);");
                        writer.WriteLine("if (data == null)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("Debug.LogError($\"ASC_ID:{id}  不存在.\");");
                        writer.WriteLine(
                            "return new AbilitySystemConfig(Array.Empty<int>(), Array.Empty<AttrSetConfig>(), Array.Empty<int>(), 0);");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine("var attrSets = new AttrSetConfig[data.AttrSet.Length];");
                        writer.WriteLine("for (var i = 0; i < data.AttrSet.Length; i++)");
                        writer.WriteLine("    attrSets[i] = XAttrSet.AttributeSetMap[data.AttrSet[i]];");

                        writer.WriteLine(
                            "return new AbilitySystemConfig(data.Tag, attrSets, data.Ability, data.Level);");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");

                    #endregion

                    writer.WriteLine("");

                    #region GameplayCue

                    writer.WriteLine("public static GameplayCueConfig GetGameplayCueConfig(int id)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        writer.WriteLine("var data = Tables.TbgameplayCue.Get(id);");
                        writer.WriteLine("if (data == null)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("Debug.LogError($\"Cue_ID:{id}  不存在.\");");
                        writer.WriteLine("return null;");
                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.WriteLine("var cueType = CueHelper.GetCueType(data.CueLogic.GetType().Name);");
                        writer.WriteLine("if (cueType == null)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine(
                            "Debug.LogError($\"Cue_ID:{id}  CueType:{data.CueLogic.GetType().Name} 不存在.\");");
                        writer.WriteLine("return null;");
                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.WriteLine("var cueLogic = data.CueLogic;");
                        writer.WriteLine("var cueLogicName = cueLogic.GetType().Name;");
                        writer.WriteLine("var cueParamType = CueHelper.GetCueLogicParamType(cueLogicName);");
                        writer.WriteLine("var cueParam = Activator.CreateInstance(cueParamType) as XParam;");
                        writer.WriteLine("if (cueParam != null)");
                        writer.WriteLine("{");
                        writer.Indent++;

                        writer.WriteLine("switch (cueLogic)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        {
                            var allCue = EditorCueHelper.GetCachedCueTypes();
                            var cueTypes = allCue as Type[] ?? allCue.ToArray();
                            foreach (var cueType in cueTypes)
                            {
                                var cueName = cueType.Name;
                                var cueParamType = EditorCueHelper.CueToCueParamTypeMap()[cueName];
                                Type tType = ReflectionHelper.GetMemberType($"cfg.{cueName}", "Param");
                                if (tType == null) continue;

                                writer.WriteLine($"case cfg.{cueName} cData:");
                                writer.WriteLine("{");
                                writer.Indent++;
                                writer.WriteLine($"var cp = cueParam as {cueParamType.FullName};");

                                // 标准 BeanField 赋值
                                var beanFields = EXEditorHelper.GetBeanFields(cueParamType);
                                foreach (var bf in beanFields)
                                    WriteFieldAssignment(writer, "cp", bf.Setter, $"cData.Param.{ToPascalCase(bf.Name)}",
                                        bf.MemberType);

                                // 多态 BeanPolymorphicField 赋值
                                var polyFields = EXEditorHelper.GetBeanPolymorphicFields(cueParamType);
                                foreach (var pf in polyFields)
                                {
                                    var (subtypes, getParamType) = GetPolymorphicHelperInfo(pf.HelperCategory);
                                    WritePolymorphicFieldAssignment(writer, "cp", $"cData.Param.{pf.BeanFieldName}", pf,
                                        subtypes, getParamType);
                                }

                                writer.WriteLine("cueParam = cp;");
                                writer.WriteLine("break;");
                                writer.Indent--;
                                writer.WriteLine("}");
                            }
                        }
                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine("(int[] all, int[] any, int[] none) ParseTagRequirement(cfg.TagRequirementSpec? requirement)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("if (requirement == null) return (Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());");
                        writer.WriteLine("var r = requirement.Value;");
                        writer.WriteLine("var all = r.All?.Where(x => x > 0).ToArray() ?? Array.Empty<int>();");
                        writer.WriteLine("var any = r.Any?.Where(x => x > 0).ToArray() ?? Array.Empty<int>();");
                        writer.WriteLine("var none = r.None?.Where(x => x > 0).ToArray() ?? Array.Empty<int>();");
                        writer.WriteLine("return (all, any, none);");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine("");
                        writer.WriteLine("var requiredTag = ParseTagRequirement(data.RequiredTag);");
                        writer.WriteLine("var immunityTag = ParseTagRequirement(data.ImmunityTag);");
                        writer.WriteLine(
                            "return new GameplayCueConfig(cueType, cueParam, requiredTag.all, requiredTag.any, requiredTag.none, immunityTag.all, immunityTag.any, immunityTag.none);");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");

                    #endregion

                    writer.WriteLine("");

                    #region GameplayEffect

                    writer.WriteLine("public static GameplayEffectConfig GetGameplayEffectConfig(int id)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        writer.WriteLine("var data = Tables.TbgameplayEffect.Get(id);");
                        writer.WriteLine("if (data == null)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("Debug.LogError($\"GameplayEffect_ID:{id}  不存在.\");");
                        writer.WriteLine("return null;");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine("");
                        writer.WriteLine("var configs = new List<GameplayEffectComponentConfig>();");
                        writer.WriteLine("");
                        writer.WriteLine("(int[] all, int[] any, int[] none)? ParseTagRequirement(cfg.TagRequirementSpec requirement)");
                        writer.WriteLine("{");
                        writer.WriteLine("");
                        writer.WriteLine("    int[] all = null, any = null, none = null;");
                        writer.WriteLine("    if(requirement.All is {Count: > 0})");
                        writer.WriteLine("        all = requirement.All.Where(x => x > 0).ToArray();");
                        writer.WriteLine("    if(requirement.Any is {Count: > 0})");
                        writer.WriteLine("        any = requirement.Any.Where(x => x > 0).ToArray();");
                        writer.WriteLine("    if(requirement.None is {Count: > 0})");
                        writer.WriteLine("        none = requirement.None.Where(x => x > 0).ToArray();");
                        writer.WriteLine("");
                        writer.WriteLine("    if(all != null && all.Length == 0) all = null;");
                        writer.WriteLine("    if(any != null && any.Length == 0) any = null;");
                        writer.WriteLine("    if(none != null && none.Length == 0) none = null;");
                        writer.WriteLine("");
                        writer.WriteLine("    if(all == null && any == null && none == null) return null;");
                        writer.WriteLine("    return (all, any, none);");
                        writer.WriteLine("}");
                        writer.WriteLine("");
                        writer.WriteLine("// assetTags");
                        writer.WriteLine("if (data.AssetTags is { Count: > 0 })");
                        writer.WriteLine("    configs.Add(new ConfAssetTags { tags = data.AssetTags.ToArray() });");
                        writer.WriteLine("// grantedTags");
                        writer.WriteLine("if (data.GrantedTags is { Count: > 0 })");
                        writer.WriteLine(
                            "    configs.Add(new ConfEffectGrantedTags { tags = data.GrantedTags.ToArray() });");
                        writer.WriteLine("// application tags condition");
                        writer.WriteLine("if (data.ApplicationRequiredTags != null)");
                        writer.WriteLine("{");
                        writer.WriteLine("    var result = ParseTagRequirement(data.ApplicationRequiredTags.Value);");
                        writer.WriteLine("    if(result != null)");
                        writer.WriteLine("        configs.Add(new ConfApplicationRequiredTags{ all = result.Value.all, any = result.Value.any, none = result.Value.none });");
                        writer.WriteLine("}");
                        writer.WriteLine("// ongoing tags condition");
                        writer.WriteLine("if (data.OngoingRequiredTags != null)");
                        writer.WriteLine("{");
                        writer.WriteLine("    var result = ParseTagRequirement(data.OngoingRequiredTags.Value);");
                        writer.WriteLine("    if(result != null)");
                        writer.WriteLine("        configs.Add(new ConfOngoingRequiredTags{ all = result.Value.all, any = result.Value.any, none = result.Value.none });");
                        writer.WriteLine("}");
                        writer.WriteLine("// remove-effect tags condition");
                        writer.WriteLine("if (data.RemoveGameplayEffectsWithTags != null)");
                        writer.WriteLine("{");
                        writer.WriteLine("    var result = ParseTagRequirement(data.RemoveGameplayEffectsWithTags.Value);");
                        writer.WriteLine("    if(result != null)");
                        writer.WriteLine("        configs.Add(new ConfRemoveEffectWithTags{ all = result.Value.all, any = result.Value.any, none = result.Value.none });");
                        writer.WriteLine("}");
                        writer.WriteLine("// immunity tags condition");
                        writer.WriteLine("if (data.ImmunityTags != null)");
                        writer.WriteLine("{");
                        writer.WriteLine("    var result = ParseTagRequirement(data.ImmunityTags.Value);");
                        writer.WriteLine("    if(result != null)");
                        writer.WriteLine("        configs.Add(new ConfEffectImmunityTags{ all = result.Value.all, any = result.Value.any, none = result.Value.none });");
                        writer.WriteLine("}");

                        writer.WriteLine("// duration");
                        writer.WriteLine("if (data.Duration != null && data.Duration.Value.Time != 0)");
                        writer.Indent++;
                        writer.WriteLine("configs.Add(new ConfDuration");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("duration = data.Duration.Value.Time,");
                        writer.WriteLine("timeUnit = (TimeUnit)data.Duration.Value.TimeUnit,");
                        writer.WriteLine(
                            "ResetStartTimeWhenActivated = data.Duration.Value.ResetStartTimeWhenActivated");
                        writer.Indent--;
                        writer.WriteLine("});");
                        writer.Indent--;

                        writer.WriteLine("// period");
                        writer.WriteLine("if (data.Period is { Time: > 0 })");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("configs.Add(new ConfPeriod");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("Period = data.Period.Value.Time,");
                        writer.WriteLine("ResetTimeCountWhenDeactivated = data.Period.Value.FirstTrigger,");
                        writer.WriteLine("GameplayEffectCodes = data.Period.Value.Effects");
                        writer.Indent--;
                        writer.WriteLine("});");
                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.WriteLine("// modifiers");
                        writer.WriteLine("if (data.Modifiers != null && data.Modifiers.Count > 0)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine(
                            "ModifierDefinitionSetting[] modifierSettings = new ModifierDefinitionSetting[data.Modifiers.Count];");
                        writer.WriteLine("for (var i = 0; i < data.Modifiers.Count; i++)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("var info = data.Modifiers[i];");
                        writer.WriteLine("modifierSettings[i] = new ModifierDefinitionSetting()");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("AttrSetCode = info.AttrSet,");
                        writer.WriteLine("AttrCode = info.Attribute,");
                        writer.WriteLine("Magnitude = info.Magnitude,");
                        writer.WriteLine("Operation = (EModifierOp)info.Operation");
                        writer.Indent--;
                        writer.WriteLine("};");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine("configs.Add(new ConfModifierConfig(){ ModifierSettings = modifierSettings });");
                        writer.Indent--;
                        writer.WriteLine("}");

                        var cueComNames = new[]
                        {
                            "CueOnApply",
                            "CueOnTick",
                            "CueOnAdd",
                            "CueOnRemove",
                            "CueOnActivate",
                            "CueOnDeactivate"
                        };
                        foreach (var cueComName in cueComNames)
                        {
                            writer.WriteLine($"// {cueComName}");
                            writer.WriteLine($"if (data.{cueComName} is {{ Count: > 0 }})");
                            writer.WriteLine("{");
                            writer.Indent++;
                            writer.WriteLine($"var cues = new GameplayCueConfig[data.{cueComName}.Count];");
                            writer.WriteLine($"for (var i = 0; i < data.{cueComName}.Count; i++)");
                            writer.WriteLine($"    cues[i] = GetGameplayCueConfig(data.{cueComName}[i]);");
                            writer.WriteLine($"configs.Add(new Conf{cueComName} {{ cues = cues }});");
                            writer.Indent--;
                            writer.WriteLine("}");
                        }

                        writer.WriteLine("// grantedAbility");
                        writer.WriteLine("if (data.GrantedAbility.Count > 0)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("var grantedAbilities = new GrantedAbilityConfigSetting[data.GrantedAbility.Count];");
                        writer.WriteLine("for (var i = 0; i < data.GrantedAbility.Count; i++)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("var info = data.GrantedAbility[i];");
                        writer.WriteLine("grantedAbilities[i] = new GrantedAbilityConfigSetting()");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("AbilityCode = info.ID,");
                        writer.WriteLine("ActivationPolicy = (GrantedAbilityActivationPolicy)info.ActivationPolicy,");
                        writer.WriteLine(
                            "DeactivationPolicy = (GrantedAbilityDeactivationPolicy)info.DeactivationPolicy,");
                        writer.WriteLine("Level = info.Level,");
                        writer.WriteLine("RemovePolicy = (GrantedAbilityRemovePolicy)info.RemovePolicy,");
                        writer.Indent--;
                        writer.WriteLine("};");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine(
                            "configs.Add(new ConfGrantedAbilityConfig() { GrantedAbilities = grantedAbilities });");
                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.WriteLine("// stacking");
                        writer.WriteLine("if (data.Stacking!=null && data.Stacking.Value.StackCode != 0)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("configs.Add(new ConfStacking()");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("StackingCode = data.Stacking.Value.StackCode,");
                        writer.WriteLine("StackType = (EffectStackType)data.Stacking.Value.StackingType,");
                        writer.WriteLine("LimitCount = data.Stacking.Value.LimitCount,");
                        writer.WriteLine(
                            "EffectDurationRefreshPolicy = (EffectDurationRefreshPolicy)data.Stacking.Value.DurationRefreshPolicy,");
                        writer.WriteLine(
                            "EffectPeriodResetPolicy = (EffectPeriodResetPolicy)data.Stacking.Value.PeriodResetPolicy,");
                        writer.WriteLine(
                            "EffectExpirationPolicy = (EffectExpirationPolicy)data.Stacking.Value.ExpirationPolicy,");
                        writer.WriteLine("denyOverflowApplication = data.Stacking.Value.DenyOverflowApplication,");
                        writer.WriteLine("clearStackOnOverflow = data.Stacking.Value.ClearStackOnOverflow,");
                        writer.WriteLine("OverflowEffectCodes = data.Stacking.Value.OverflowEffects");
                        writer.Indent--;
                        writer.WriteLine("});");
                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.WriteLine("");
                        writer.WriteLine("return new GameplayEffectConfig(configs.ToArray());");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");

                    #endregion

                    writer.WriteLine("");

                    #region Ability

                    writer.WriteLine("public static AbilityConfig GetAbilityConfig(int id)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        writer.WriteLine("var data = Tables.Tbability.Get(id);");
                        writer.WriteLine("if (data == null)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("Debug.LogError($\"Ability_ID:{id}  不存在.\");");
                        writer.WriteLine("return null;");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine("");

                        writer.WriteLine("var configs = new List<AbilityComponentConfig>();");
                        writer.WriteLine("");

                        writer.WriteLine("// baseInfo");
                        writer.WriteLine("configs.Add(new ConfAbilityBaseInfo { Code = id, Level = 0 });");

                        writer.WriteLine("// cost");
                        writer.WriteLine("if (data.Cost != 0)");
                        writer.WriteLine(
                            "    configs.Add(new ConfAbilityCost{ GameplayEffectCode = data.Cost });");

                        writer.WriteLine("// assetTags");
                        writer.WriteLine("if (data.AssetTags is { Count: > 0 })");
                        writer.WriteLine(
                            "    configs.Add(new ConfAbilityAssetTags { tags = data.AssetTags.ToArray() });");

                        writer.WriteLine("(int[] all, int[] any, int[] none)? ParseTagRequirement(cfg.TagRequirementSpec? requirement)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("if (requirement == null) return null;");
                        writer.WriteLine("var r = requirement.Value;");
                        writer.WriteLine("int[] all = null, any = null, none = null;");
                        writer.WriteLine("if (r.All is { Count: > 0 }) all = r.All.Where(x => x > 0).ToArray();");
                        writer.WriteLine("if (r.Any is { Count: > 0 }) any = r.Any.Where(x => x > 0).ToArray();");
                        writer.WriteLine("if (r.None is { Count: > 0 }) none = r.None.Where(x => x > 0).ToArray();");
                        writer.WriteLine("if (all == null && any == null && none == null) return null;");
                        writer.WriteLine("return (all, any, none);");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine("");
                        writer.WriteLine("int[] PickSimpleTagSet((int[] all, int[] any, int[] none) req)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("if (req.any is { Length: > 0 }) return req.any;");
                        writer.WriteLine("if (req.all is { Length: > 0 }) return req.all;");
                        writer.WriteLine("if (req.none is { Length: > 0 }) return req.none;");
                        writer.WriteLine("return Array.Empty<int>();");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine("");

                        writer.WriteLine("// cancelAbilityWithTags");
                        writer.WriteLine("var cancelTags = ParseTagRequirement(data.CancelAbilityWithTags);");
                        writer.WriteLine("if (cancelTags != null)");
                        writer.WriteLine(
                            "    configs.Add(new ConfCancelAbilityWithTags { tags = PickSimpleTagSet(cancelTags.Value) });");

                        writer.WriteLine("// blockAbilityWithTags");
                        writer.WriteLine("var blockTags = ParseTagRequirement(data.BlockAbilityWithTags);");
                        writer.WriteLine("if (blockTags != null)");
                        writer.WriteLine(
                            "    configs.Add(new ConfBlockAbilityWithTags { tags = PickSimpleTagSet(blockTags.Value) });");

                        writer.WriteLine("// activationOwnedTags");
                        writer.WriteLine("if (data.ActivationOwnedTags is { Count: > 0 })");
                        writer.WriteLine(
                            "    configs.Add(new ConfAbilityActivationOwnedTags { tags = data.ActivationOwnedTags.ToArray() });");

                        writer.WriteLine("// activationRequiredTags");
                        writer.WriteLine("var activationRequiredTags = ParseTagRequirement(data.ActivationRequiredTags);");
                        writer.WriteLine("if (activationRequiredTags != null)");
                        writer.WriteLine(
                            "    configs.Add(new ConfAbilityActivationRequiredTags { all = activationRequiredTags.Value.all, any = activationRequiredTags.Value.any, none = activationRequiredTags.Value.none });");

                        writer.WriteLine("// activationBlockedTags");
                        writer.WriteLine("var activationBlockedTags = ParseTagRequirement(data.ActivationBlockedTags);");
                        writer.WriteLine("if (activationBlockedTags != null)");
                        writer.WriteLine(
                            "    configs.Add(new ConfAbilityActivationBlockedTags { all = activationBlockedTags.Value.all, any = activationBlockedTags.Value.any, none = activationBlockedTags.Value.none });");

                        writer.WriteLine("// cdEffect cd");
                        writer.WriteLine("if (data.Cd != 0)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("configs.Add(new ConfAbilityCooldown");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("Cooldown = data.Cd,");
                        writer.WriteLine("GameplayEffectCode = data.CdEffect");
                        writer.Indent--;
                        writer.WriteLine("});");
                        writer.Indent--;
                        writer.WriteLine("}");

                        writer.WriteLine("// abilityExecution");
                        writer.WriteLine("switch (data.AbilityExecution)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine("case cfg.ApplyEffectsOnActivate aData:");
                        writer.WriteLine("    configs.Add(new ConfAbilityEffectsOnActivate { EffectCodes = aData.Param.IDs });");
                        writer.WriteLine("    break;");
                        writer.WriteLine("case cfg.TimelineRef aData:");
                        writer.WriteLine("    configs.Add(new ConfAbilityTargetEffectsOnActivate");
                        writer.WriteLine("    {");
                        writer.WriteLine("        EffectCodes = CollectTimelineApplyEffectCodes(aData.Param.ID),");
                        writer.WriteLine("        AutoEndOnCommit = true");
                        writer.WriteLine("    });");
                        writer.WriteLine("    break;");
                        writer.WriteLine("case cfg.MoveInput aData:");
                        writer.WriteLine("    configs.Add(new ConfAbilityMoveInput { RotationOffset = aData.Param.RotationOffset });");
                        writer.WriteLine("    break;");
                        writer.WriteLine("default:");
                        writer.WriteLine("    Debug.LogError($\"Ability_ID:{id}  AbilityExecution:{data.AbilityExecution?.GetType().Name ?? \\\"null\\\"} 不存在.\");");
                        writer.WriteLine("    break;");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine("");
                        writer.WriteLine("return new AbilityConfig(configs.ToArray());");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");

                    #endregion

                    writer.WriteLine("");

                    writer.WriteLine("private static int[] CollectTimelineApplyEffectCodes(int timelineId)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("var timeline = Tables.TbtimelineAbility.GetOrDefault(timelineId);");
                    writer.WriteLine("if (timeline == null)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("Debug.LogError($\"TimelineAbility_ID:{timelineId}  不存在.\");");
                    writer.WriteLine("return Array.Empty<int>();");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine("");
                    writer.WriteLine("var effects = new List<int>();");
                    writer.WriteLine("if (timeline.Tracks == null)");
                    writer.Indent++;
                    writer.WriteLine("return Array.Empty<int>();");
                    writer.Indent--;
                    writer.WriteLine("");
                    writer.WriteLine("for (var trackIndex = 0; trackIndex < timeline.Tracks.Length; trackIndex++)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("var track = timeline.Tracks[trackIndex];");
                    writer.WriteLine("if (track?.ActionClips == null)");
                    writer.Indent++;
                    writer.WriteLine("continue;");
                    writer.Indent--;
                    writer.WriteLine("");
                    writer.WriteLine("for (var clipIndex = 0; clipIndex < track.ActionClips.Length; clipIndex++)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("if (track.ActionClips[clipIndex]?.Action is not cfg.ApplyEffects applyEffects)");
                    writer.Indent++;
                    writer.WriteLine("continue;");
                    writer.Indent--;
                    writer.WriteLine("");
                    writer.WriteLine("var ids = applyEffects.Param?.IDs;");
                    writer.WriteLine("if (ids == null)");
                    writer.Indent++;
                    writer.WriteLine("continue;");
                    writer.Indent--;
                    writer.WriteLine("");
                    writer.WriteLine("for (var effectIndex = 0; effectIndex < ids.Length; effectIndex++)");
                    writer.Indent++;
                    writer.WriteLine("if (ids[effectIndex] > 0) effects.Add(ids[effectIndex]);");
                    writer.Indent--;
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine("");
                    writer.WriteLine("return effects.Count == 0 ? Array.Empty<int>() : effects.ToArray();");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine("");

                    #region TimelineAbility

                    WriteTimelineAbilityConfigMethods(writer);

                    #endregion

                    writer.WriteLine("");

                    #region Utils

                    writer.WriteLine("public static string GetAbilityNameByCode(int id)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        writer.WriteLine("var data = Tables.Tbability.Get(id);");
                        writer.WriteLine("if (data != null) return data.Name;");
                        writer.WriteLine("Debug.LogError($\"Ability_ID:{id}  不存在.\");");
                        writer.WriteLine("return string.Empty;");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine("");

                    writer.WriteLine("public static string GetAttrSetNameByCode(int code)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        writer.WriteLine("var data = Tables.TbattributeSet.Get(code);");
                        writer.WriteLine("if (data != null) return data.Name;");
                        writer.WriteLine("Debug.LogError($\"AttrSet_Code:{code}  不存在.\");");
                        writer.WriteLine("return string.Empty;");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine("");

                    writer.WriteLine("public static string GetAttributeNameByCode(int code)");
                    writer.WriteLine("{");
                    writer.Indent++;
                    {
                        writer.WriteLine("var data = Tables.Tbattribute.Get(code);");
                        writer.WriteLine("if (data != null) return data.Name;");
                        writer.WriteLine("Debug.LogError($\"Attribute_Code:{code}  不存在.\");");
                        writer.WriteLine("return string.Empty;");
                    }
                    writer.Indent--;
                    writer.WriteLine("}");

                    #endregion

                }
                writer.Indent--;
                writer.WriteLine("}");
            }
            writer.Indent--;
            writer.WriteLine("}");
        }
    }
}

