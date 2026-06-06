# GAS CodeGen Drivers

`Generate-GAS-SourceGen.bat` is the fast iteration driver. It does not start Unity:

1. Luban JSON / C# export
2. dotnet-hosted `GasCodeGenPipeline`
3. generated manifest and validation report

Current sourcegen boundary: this path consumes Luban Excel output / JSON and generator templates directly. It is intended for template/runtime iteration and static gate checks. It does not run `BeanUpdater`, does not refresh `AssetDatabase`, does not validate Unity asmdef import, and does not run Editor-only discovery.

Usage:

```bat
Tools\CodeGen\Generate-GAS-SourceGen.bat
dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- --projectRoot "%CD%" --mode sourcegen
```

`Generate-GAS-CodeGen.bat` is the Unity-domain verification driver. It invokes Unity in batchmode through `GAS.Editor.GasCodeGenBatchRunner.GenerateAllAndExit`, so it does not require opening the Editor UI or clicking the menu item.

Use it when you need `GASSettingAsset`, `BeanUpdater`, Unity compilation domain, asmdef import, or AssetDatabase refresh validation:

```bat
Tools\CodeGen\Generate-GAS-CodeGen.bat
Tools\CodeGen\Generate-GAS-CodeGen.bat E:\Unity\UnityEditor\6000.3.14f1\Editor\Unity.exe
dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- --projectRoot "%CD%" --mode unity --unity "E:\Unity\UnityEditor\6000.3.14f1\Editor\Unity.exe"
```
