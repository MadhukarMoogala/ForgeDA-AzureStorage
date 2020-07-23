# Forge Design Automation-Integrate with Azure Storage

### Instructions To Build and Test Forge DA Client

This is Design Automation client is used to run forge Design Application,  that can upload an input file to Azure Blob container, change its `width` and `height` parameter and save the output file to Azure container, download locally.

 DA client is highly customizable CLI utility can be used for various DA apps. At a broad level,

- inputs are uploaded to Azure storage container,
- generates a readable signed url
- output objects are uploaded to writeable azure SAS url
- urls are set in workItem spec

```
cd da
touch appsettings.users.json `feed with your Forge Credentials, Azure credentials`
dotnet build
dotnet run  -i "<inputFile>" -o "<outputFolder>"
```
appsettings.users.json
```
{
  "Forge": {
    "ClientId": "",
    "ClientSecret": ""
  },
  "AzureCloudStorage": {
    "StorageConnectionString": ""
  }
}
```
launchsettings.json
```
{
  "profiles": {
    "da": {
      "commandName": "Project",
      "commandLineArgs": "-i "input.ipt" -o "output",
      "workingDirectory": ".\\cli\\da",
      "environmentVariables": {
      	"FORGE_CLIENT_ID": "",
        "FORGE_CLIENT_SECRET": "",
        "STORAGECONNECTIONSTRING": ""
        
      }
    }
  }
}
```

#### Instructions To Debug

##### .NETCore

```
edit launchsettings.json
launch da profile.
```

![InstructionImage](https://github.com/MadhukarMoogala/ForgeDA-AzureStorage/blob/master/instruction.png)

### Demo
[![asciicast](https://asciinema.org/a/349153.svg)](https://asciinema.org/a/349153)

### License

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT). Please see the [LICENSE](https://github.com/MadhukarMoogala/acadio-snippets/blob/v3/LICENSE) file for full details.

### Written by

Madhukar Moogala, [Forge Partner Development](http://forge.autodesk.com/) @galakar