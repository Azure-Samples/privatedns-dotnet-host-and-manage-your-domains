---
page_type: sample
languages:
- csharp
products:
- azure
extensions:
- services: Private-Dns
- platforms: dotnet
---

# Getting started on hosting and managing your domains in C# #

 Azure private DNS sample for managing DNS zones.
  - Creates a private DNS zone (private.contoso.com)
  - Creates a virtual network
  - Link a virtual network
  - Creates test virtual machines
  - Creates an additional DNS record
  - Test the private DNS zone


## Running this Sample ##

To run this sample:

Set the environment variable `AZURE_AUTH_LOCATION` with the full path for an auth file. See [how to create an auth file](https://github.com/Azure/azure-libraries-for-net/blob/master/AUTH.md).

    git clone https://github.com/Azure-Samples/privatedns-dotnet-host-and-manage-your-domains.git

    cd privatedns-dotnet-host-and-manage-your-domains

    dotnet build

    bin\Debug\net452\ManagePrivateDns.exe

## More information ##

[Azure Management Libraries for C#](https://github.com/Azure/azure-sdk-for-net/tree/Fluent)
[Azure .Net Developer Center](https://azure.microsoft.com/en-us/develop/net/)
If you don't have a Microsoft Azure subscription you can get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212)

---

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.