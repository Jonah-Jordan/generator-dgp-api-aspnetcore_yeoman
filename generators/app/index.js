'use strict';
var Generator = require('yeoman-generator');
var chalk = require('chalk');
var yosay = require('yosay');
var del = require('del');
var nd = require('node-dir');
var uuidv1 = require('uuid/v1');
var updateNotifier = require('update-notifier');
var pkg = require('./../../package.json');
// eslint-disable-next-line no-unused-vars
var path = require('path');

module.exports = class extends Generator {

  constructor(args, opts) {
    super(args, opts);
  }

  initializing() {
  }

  prompting() {
    //var done = this.async();

    // greet the user
    this.log(yosay('Welcome to the fantastic Yeoman ' + chalk.green('dgp-api-aspnetcore') + ' ' + chalk.blue('(' + pkg.version + ')') + ' generator!'));

    var notifier = updateNotifier({
      pkg,
      updateCheckInterval: 1000 * 60 * 5 // check every 5 minutes.
    });
    notifier.notify();
    if (notifier.update !== undefined) {
      return;
    }

    // ask project parameters
    var prompts = [{
      type: 'input',
      name: 'deleteContent',
      message: 'Delete the contents of this directory before generation (.git will be preserved)? (y/n):',
      default:
				'y'
    }, {
      type: 'input',
      name: 'projectName',
      message: 'Enter the name of the new project (PascalCasing, e.g. "MyProjectApi"):'
    }, {
      type: 'input',
      name: 'kestrelHttpPort',
      message: 'Enter the HTTP port for the kestrel server (use the port assigned by AppConfig + 1):'
    }, {
      type: 'input',
      name: 'iisHttpPort',
      message: 'Enter the HTTP port for the IIS Express server (use the port assigned by AppConfig):'
    }, {
      type: 'input',
      name: 'iisHttpsPort',
      message: 'Enter the HTTPS port for the IIS Express server (last 2 characters of the port assigned by AppConfig with 443 as prefix):'
    }, {
      type: 'input',
      name: 'dataProvider',
      message: 'Which data provider will you be using? MongoDB, MSSQL, PostgreSQL or Not ? (mo/ms/p/n):',
      default:
				'p'
    }
    ];

    return this.prompt(prompts).then(answers => {
      this.props = answers; // To access answers later use this.props.someOption;
      //done();
    });
  }

  writing() {

    // empty target directory
    console.log('Emptying target directory...');
    if (this.props.deleteContent === 'y') {
      del.sync(['**/*', '!.git', '!.git/**/*'], {
        force: true,
        dot: true
      });
    }

    var projectName = this.props.projectName;
    var lowerProjectName = projectName.toLowerCase();

    var solutionItemsGuid = uuidv1(); console.log('solutionItemsGuid: ' + solutionItemsGuid + '\r\n');
    var srcGuid = uuidv1();
    var testGuid = uuidv1();
    var starterKitGuid = uuidv1();
    var integrationGuid = uuidv1();
    var unitGuid = uuidv1();

    var kestrelHttpPort = this.props.kestrelHttpPort;
    var iisHttpPort = this.props.iisHttpPort;
    var iisHttpsPort = this.props.iisHttpsPort;
    var dataProvider = getDataProvider(this.props.dataProvider, projectName);

    var copyOptions = {
      process: function (contents) {
        var str = contents.toString();
        var result = str
          .replace(/StarterKit/g, projectName)
          .replace(/starterkit/g, lowerProjectName)
          .replace(/DataAccessSettingsNpg/g, 'DataAccessSettings')
          .replace(/DataAccessSettingsMs/g, 'DataAccessSettings')
          .replace(
            /DataAccessSettingsConfigKeyMs/g,
            'DataAccessSettingsConfigKey'
          )
          .replace(
            /DataAccessSettingsConfigKeyNpg/g,
            'DataAccessSettingsConfigKey'
          )
          .replace(
            /C3E0690A-0044-402C-90D2-2DC0FF14980F/g,
            solutionItemsGuid.toUpperCase()
          )
          .replace(
            /05A3A5CE-4659-4E00-A4BB-4129AEBEE7D0/g,
            srcGuid.toUpperCase()
          )
          .replace(
            /079636FA-0D93-4251-921A-013355153BF5/g,
            testGuid.toUpperCase()
          )
          .replace(
            /BD79C050-331F-4733-87DE-F650976253B5/g,
            starterKitGuid.toUpperCase()
          )
          .replace(
            /948E75FD-C478-4001-AFBE-4D87181E1BEC/g,
            integrationGuid.toUpperCase()
          )
          .replace(
            /0A3016FD-A06C-4AA1-A843-DEA6A2F01696/g,
            unitGuid.toUpperCase()
          )
          .replace(
            /http:\/\/localhost:51002/g,
            'http://localhost:' + kestrelHttpPort
          )
          .replace(
            /http:\/\/localhost:51001/g,
            'http://localhost:' + iisHttpPort
          )
          .replace(/"sslPort": 44300/g, '"sslPort": ' + iisHttpsPort)
          .replace(/<!-- dataaccess-package -->/g, dataProvider.package)
          .replace(
            /\/\/--dataaccess-startupImports--/g,
            dataProvider.startupImports
          )
          .replace(
            /\/\/--dataaccess-startupServices--/g,
            dataProvider.startupServices
          )
          .replace(
            /\/\/--dataaccess-registerConfiguration--/g,
            dataProvider.registerConfiguration
          )
          .replace(/\/\/--dataaccess-variable--/g, dataProvider.variable)
          .replace(/\/\/--dataaccess-getService--/g, dataProvider.getService)
          .replace(/\/\/--dataaccess-config--/g, dataProvider.programConfig)
          .replace(/<!-- dataaccess-tools -->/g, dataProvider.tools);
        return result;
      }
    };

    var source = this.sourceRoot();
    var dest = this.destinationRoot();
    var fs = this.fs;

    // copy files and rename starterkit to projectName

    console.log('Creating project skeleton...');

    nd.files(source, function (err, files) {
      for (var i = 0; i < files.length; i++) {
        var ignoreFiles = [];
        var filename = files[i]
          .replace(/StarterKit/g, projectName)
          .replace(/starterkit/g, lowerProjectName)
          .replace('.npmignore', '.gitignore')
          .replace('dataaccess.ms.json', 'dataaccess.json')
          .replace('dataaccess.npg.json', 'dataaccess.json')
          .replace('DataAccessSettings.ms.cs', 'DataAccessSettings.cs')
          .replace('DataAccessSettings.npg.cs', 'DataAccessSettings.cs')
          .replace('DataAccessSettingsConfigKey.ms.cs', 'DataAccessSettingsConfigKey.cs')
          .replace('DataAccessSettingsConfigKey.npg.cs', 'DataAccessSettingsConfigKey.cs')
          .replace(source, dest);
        switch (dataProvider.input) {
        case 'p':
          if (files[i].indexOf('dataaccess.ms.json') > -1) {
            ignoreFiles.push(files[i]);
          }
          if (files[i].indexOf('DataAccessSettings.ms.cs') > -1) {
            ignoreFiles.push(files[i]);
          }
          if (files[i].indexOf('DataAccessSettingsConfigKey.ms.cs') > -1) {
            ignoreFiles.push(files[i]);
          }
          break;
        case 'ms':
          if (files[i].indexOf('dataaccess.npg.json') > -1) {
            ignoreFiles.push(files[i]);
          }
          if (files[i].indexOf('DataAccessSettings.npg.cs') > -1) {
            ignoreFiles.push(files[i]);
          }
          if (files[i].indexOf('DataAccessSettingsConfigKey.npg.cs') > -1) {
            ignoreFiles.push(files[i]);
          }
          break;
        default:
          if (
            files[i].indexOf('EntityContext.cs') > -1 ||
            files[i].indexOf('DataAccessDefaults.cs') > -1 ||
            files[i].indexOf('dataaccess.ms.json') > -1 ||
            files[i].indexOf('dataaccess.npg.json') > -1 ||
            files[i].indexOf('DataAccessSettings.ms.cs') > -1 ||
            files[i].indexOf('DataAccessSettings.npg.cs') > -1 ||
            files[i].indexOf('DataAccessSettingsConfigKey.ms.cs') > -1 ||
            files[i].indexOf('DataAccessSettingsConfigKey.npg.cs') > -1
          ) {
            ignoreFiles.push(files[i]);
          }
        }

        if (!ignoreFiles.includes(files[i])) {
          fs.copy(files[i], filename, copyOptions);
        }
      }
    });

  }

  install() {
    // this.installDependencies();
    //this.log('----');
  }
};

function getDataProvider(input, projectName) {
  var efCorePackage =
      '<PackageReference Include="Microsoft.EntityFrameworkCore" Version="3.1.9" />\n';
  var efDesignPackage =
      '<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="3.1.9">\n' +
      '<PrivateAssets>all</PrivateAssets>\n' +
      '<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>\n' +
      '</PackageReference>\n';
  var npgSqlPackage =
    '<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="3.1.9" />\n';
  var sqlServerPackage =
    '<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="3.1.9" />\n';
  var usings = 'using Microsoft.EntityFrameworkCore;\nusing Microsoft.EntityFrameworkCore.Migrations;\nusing Digipolis.DataAccess;\nusing StarterKit.DataAccess;\nusing StarterKit.DataAccess.Options;\nusing Microsoft.EntityFrameworkCore.Diagnostics;'.replace(/StarterKit/g, projectName);
  var programConfig = 'config.AddJsonFile(JsonFilesKey.DataAccessJson);\n';
  var tools =
      '<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="3.1.9">\n' +
      '<PrivateAssets>all</PrivateAssets>\n' +
      '<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>\n' +
      '</PackageReference>\n';
  var registerConfiguration =
    'DataAccessSettings.RegisterConfiguration(services, Configuration.GetSection(Shared.Constants.ConfigurationSectionKey.DataAccess), Environment);';
  var variable = 'DataAccessSettings dataAccessSettings;';
  var getService =
    'dataAccessSettings = provider.GetService<IOptions<DataAccessSettings>>().Value;';

  var dataProvider = {
    input: input,
    package: '',
    startupServices: '',
    startupImports: '',
    programConfig: '',
    connString: '',
    tools: '',
    registerConfiguration: '',
    variable: '',
    getService: ''
  };

  if (input.toLowerCase() === 'p') {
    dataProvider.package = efCorePackage + efDesignPackage + npgSqlPackage;
    dataProvider.startupServices =
			'      services.AddDataAccess<EntityContext>()\n' +
			'      .AddDbContext<EntityContext>(options => {\n' +
			'      		options.UseNpgsql(dataAccessSettings.GetConnectionString(),\n' +
			'      		opt => opt.MigrationsHistoryTable(HistoryRepository.DefaultTableName, DataAccessDefaults.SchemaName));\n' +
			'      });';

    dataProvider.startupImports = usings;
    dataProvider.programConfig = programConfig;
    dataProvider.tools = tools;
    dataProvider.registerConfiguration = registerConfiguration;
    dataProvider.variable = variable;
    dataProvider.getService = getService;
  } else if (input.toLowerCase() === 'ms') {
    dataProvider.package = efCorePackage + efDesignPackage + sqlServerPackage;
    dataProvider.startupServices =
		'      services.AddDataAccess<EntityContext>()\n' +
		'      .AddDbContext<EntityContext>(options => {\n' +
		'      		options.UseSqlServer(dataAccessSettings.GetConnectionString());\n' +
		'      });';

    dataProvider.startupImports = usings;
    dataProvider.programConfig = programConfig;
    dataProvider.tools = tools;
    dataProvider.registerConfiguration = registerConfiguration;
    dataProvider.variable = variable;
    dataProvider.getService = getService;
  }

  return dataProvider;
}
