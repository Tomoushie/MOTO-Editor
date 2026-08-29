
### 1.7 `Moto.Plugin.Template/Moto.Plugin.Template.nuspec`

```xml
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>Moto.Plugin.Template</id>
    <version>1.0.0</version>
    <authors>Tom NOWAK</authors>
    <description>Template dotnet pour créer des plugins MOTO Editor</description>
    <tags>moto plugin template editor</tags>
    <packageTypes>
      <packageType name="Template" />
    </packageTypes>
  </metadata>
  <files>
    <file src="content\**\*.*" target="content" />
    <file src=".template.config\template.json" target="content\.template.config" />
  </files>
</package>
