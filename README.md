# Kastra.Web

[![Join the chat at https://gitter.im/kastra-web/community](https://badges.gitter.im/kastra-web/community.svg)](https://gitter.im/kastra-web/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

## Minimum requirements

* .NET Core Runtime 3.1
* SQL Server Database 2008 or higher
* IIS or Nginx or Apache server

## Installation guide

* Configure the server to host a .NET Core website, you can follow one of these documentations :
    - [Host ASP.NET Core on Windows with IIS](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-2.2)
    - [Host ASP.NET Core on Linux with Nginx](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-2.2)
    - [Host ASP.NET Core on Linux with Apache](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-apache?view=aspnetcore-2.2)

* Configure the SQL Server database. You can use one of these authentication methods to connect your website to the database :
  - SQL Server account
  - Integrated Windows Authentication

* Unzip the Kastra website archive in the website folder on the server. Ensure that the user or the pool which executes the website has the write permission on the appsettings.json file.

* Go to the installation url `https://<your-domain>/install/index` and fill the information.

* Now, the website is installed, you can configure it by editing the appsettings.json file.

## Configure the application settings

* Open the AppSettings.json file and edit the settings.

* To apply the new settings, restart the website.

### Available settings

* **ConnectionStrings** : Connection strings to connect the website to the database. It is configured automatically by the Kastra setup.

* **Log4net** : Settings for Log4net, read the Log4net documentation for more information or you can read this [documentation](https://github.com/huorswords/Microsoft.Extensions.Logging.Log4Net.AspNetCore/blob/develop/doc/CONFIG.md).

* **Configuration** (Kastra configuation parameters)
  - **DevelopmentMode** : Enable the development mode. For more security, it should be set to false in production.
  - **BusinessDllPath** : Required path to load the Businness layer DLL file (by default : Kastra.Business.dll).
  - **DALDllPath** : Required path to load the Data Access layer DLL file (by default : Kastra.DAL.EntityFramework.dll).
  - **ModuleDirectoryPath** : Required path to the module folder.
  - **EnableDatabaseUpdate** : Enable the automatic database migration update for the Kastra website and the installed modules when the website starts.

* **Cors** : Settings the configure the CORS policies
  - **EnableCors** : Parameter to enable the CORS
  - **AllowAnyOrigin** : Parameter to allow any origin, it should be set to true for development.
  - **Origins** : Parameter to set the allowed origins

### Example of appsettings.json

*appsettings.json*
```Json
{
  "ConnectionStrings": { "DefaultConnection": "" },
  "Log4net": [
    {
      "XPath": "/log4net/appender[@name='RollingFile']/file",
      "Attributes": {
        "Value": "Logs/Kastra.Web.log"
      }
    },
    {
      "XPath": "/log4net/appender[@name='RollingFile']/maximumFileSize",
      "Attributes": {
        "Value": "1024KB"
      }
    }
  ],
  "AppSettings": {
    "Configuration": {
      "BusinessDllPath": "Kastra.Business.dll",
      "DALDllPath": "Kastra.DAL.EntityFramework.dll",
      "ModuleDirectoryPath": "Modules"
    },
    "Cors": {
      "EnableCors": true,
      "Origins": "<your-domain>,<your-domain1>,<your-domain2>",
      "AllowAnyOrigin": false
    }
  }
}
```




