self.webfilelist['TriggitFeedGenerator']=self.DevGetFileList(base='src\\TriggitFeedGenerator\\bin\\$MODE',
                         xfiles=['buildinfo.xml',"*.resx","Default2.aspx","packages.config","*.targets","Web.Debug.Config","Web.Release.Config"])
self.webreplace['TriggitFeedGenerator']={
    'TriggitFeedGenerator.exe.config':
    {
		"/configuration/connectionStrings/add[@name='BatchDbConnectionString']/@connectionString"   	:"BatchDB",   
		"/configuration/connectionStrings/add[@name='SearchData.DbConnection']/@connectionString"   	:"OdysseyCommerceDB",  
		"/configuration/connectionStrings/add[@name='ProductData.DbConnection']/@connectionString"      :"ProductsDB", 
		"/configuration/appSettings/add[@key='AllowIncrementalRuns']/@value"							:"AllowIncrementalRuns",
		"/configuration/appSettings/add[@key='AllowItemErrorsInFiles']/@value"							:"AllowItemErrorsInFiles",
		"/configuration/appSettings/add[@key='AllowDeletedCategories']/@value"							:"AllowDeletedCategories",
		"/configuration/appSettings/add[@key='AllowRuleOptimizations']/@value"							:"AllowRuleOptimizations",
		"/configuration/appSettings/add[@key='AllowRuleEntryRemovals']/@value"							:"AllowRuleEntryRemovals",
		"/configuration/appSettings/add[@key='AllowIEnumerableRuleEvaluations']/@value"					:"AllowIEnumerableRuleEvaluations",
		"/configuration/appSettings/add[@key='MaximumThreadsToUse']/@value"								:"MaximumThreadsToUse",
		"/configuration/appSettings/add[@key='LimitTo100Products']/@value"								:"LimitTo100Products",
		"/configuration/appSettings/add[@key='BaseUrl']/@value"											:"BaseUrl",
		"/configuration/appSettings/add[@key='ImgBaseUrl']/@value"										:"ImgBaseUrl",
		"/configuration/appSettings/add[@key='GzipFiles']/@value"										:"GzipFiles",
		"/configuration/appSettings/add[@key='FtpHost']/@value"											:"FtpHost",
		"/configuration/appSettings/add[@key='FtpDropFolderPath']/@value"								:"FtpDropFolderPath",
		"/configuration/appSettings/add[@key='FtpUserName']/@value"										:"FtpUserName",
		"/configuration/appSettings/add[@key='FtpUserPassword']/@value"									:"FtpUserPassword",
		"/configuration/appSettings/add[@key='OutputFolderPath']/@value"								:"OutputFolderPath",
		"/configuration/appSettings/add[@key='SkipHasImageCheck']/@value"								:"SkipHasImageCheck", 
		"/configuration/appSettings/add[@key='TestIncrementalRunFromDate']/@value"						:"TestIncrementalRunFromDate",
		"/configuration/appSettings/add[@key='OutputFileMoveMethod']/@value"							:"OutputFileMoveMethod",
		"/configuration/appSettings/add[@key='TargetFolderPath']/@value"								:"TargetFolderPath",
		"/configuration/appSettings/add[@key='InputFolderPath']/@value"									:"InputFolderPath",
    },
	'Log4net.config':
	{
		"/configuration/log4net/appender[@name='SmtpAppender']/smtpHost/@value"                         :"SMTPHost",
		"/configuration/log4net/appender[@name='SmtpAppender']/to/@value"                            	:"SMTPTo",
		"/configuration/log4net/appender[@name='SmtpAppender']/from/@value"                           	:"SMTPFrom",
		"/configuration/log4net/appender[@name='SmtpAppender']/subject/@value"                        	:"SMTPSubject",
	},
}

self.webfilelist['GoogleCategoryImporter']=self.DevGetFileList(base='src\\GoogleCategoryImporter\\bin\\$MODE',
                         xfiles=['buildinfo.xml',"*.resx","Default2.aspx","packages.config","*.targets","Web.Debug.Config","Web.Release.Config"])
self.webreplace['GoogleCategoryImporter']={
    'GoogleCategoryImporter.exe.config':
    {
		"/configuration/connectionStrings/add[@name='BatchDbConnectionString']/@connectionString"   	:"BatchDB",  
		"/configuration/appSettings/add[@key='InputFileFolderPath']/@value"								:"InputFileFolderPath",
    },
	'Log4net.config':
	{
		"/configuration/log4net/appender[@name='SmtpAppender']/smtpHost/@value"                         :"SMTPHost",
		"/configuration/log4net/appender[@name='SmtpAppender']/to/@value"                            	:"SMTPTo",
		"/configuration/log4net/appender[@name='SmtpAppender']/from/@value"                           	:"SMTPFrom",
		"/configuration/log4net/appender[@name='SmtpAppender']/subject/@value"                          :"SMTPSubject",
	},
}
self.webfilelist['IndigoFeedSystemDataProcessor']=self.DevGetFileList(base='src\\IndigoFeedSystemDataProcessor\\bin\\$MODE',
                         xfiles=['buildinfo.xml',"*.resx","Default2.aspx","packages.config","*.targets","Web.Debug.Config","Web.Release.Config"])
self.webreplace['IndigoFeedSystemDataProcessor']={
    'IndigoFeedSystemDataProcessor.exe.config':
    {
		"/configuration/connectionStrings/add[@name='BatchDbConnectionString']/@connectionString"   								:"BatchDB",  
		"/configuration/connectionStrings/add[@name='CmsDbConnectionString']/@connectionString"      								:"EktronDB",
		"/configuration/connectionStrings/add[@name='RecosDBConnectionString']/@connectionString"      								:"RecosDB",
		"/configuration/appSettings/add[@key='EndecaUrl']/@value"																	:"EndecaUrl",
		"/configuration/appSettings/add[@key='EndecaPort']/@value"																	:"EndecaPort",
		
		"/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_ICatalogueServiceContract']/@address"        :"NetNamedPipeBinding_ICatalogueServiceContract",
        "/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_ICustomerListServiceContract']/@address"     :"NetNamedPipeBinding_ICustomerListServiceContract",
        "/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_IMerchAdminServiceContract']/@address"       :"NetNamedPipeBinding_IMerchAdminServiceContract",
        "/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_IMerchandisingServiceContract']/@address"    :"NetNamedPipeBinding_IMerchandisingServiceContract",
        "/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_IOrderServiceContract']/@address"            :"NetNamedPipeBinding_IOrderServiceContract",
        "/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_IProfileServiceContract']/@address"          :"NetNamedPipeBinding_IProfileServiceContract",
        "/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_IReferenceDataServiceContract']/@address"    :"NetNamedPipeBinding_IReferenceDataServiceContract",
        "/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_ISocialServiceContract']/@address"           :"NetNamedPipeBinding_ISocialServiceContract",
        "/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_IStoreServiceContract']/@address"            :"NetNamedPipeBinding_IStoreServiceContract",
        "/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_IAdminServiceContract']/@address"            :"NetNamedPipeBinding_IAdminServiceContract",
        "/configuration/system.serviceModel/client/endpoint[@name='NetNamedPipeBinding_IEcommerceServiceContract']/@address"        :"NetNamedPipeBinding_IEcommerceServiceContract",

		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_ICatalogueServiceContract']/@address"              :"netTcpBinding_ICatalogueServiceContract",
		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_ICustomerListServiceContract']/@address"           :"netTcpBinding_ICustomerListServiceContract",
		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_IMerchAdminServiceContract']/@address"             :"netTcpBinding_IMerchAdminServiceContract",
		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_IMerchandisingServiceContract']/@address"          :"netTcpBinding_IMerchandisingServiceContract",
		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_IOrderServiceContract']/@address"                  :"netTcpBinding_IOrderServiceContract",
		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_IProfileServiceContract']/@address"                :"netTcpBinding_IProfileServiceContract",
		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_IReferenceDataServiceContract']/@address"          :"netTcpBinding_IReferenceDataServiceContract",
		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_ISocialServiceContract']/@address"                 :"netTcpBinding_ISocialServiceContract",
		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_IStoreServiceContract']/@address"                  :"netTcpBinding_IStoreServiceContract",
		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_IAdminServiceContract']/@address"                  :"netTcpBinding_IAdminServiceContract",
		"/configuration/system.serviceModel/client/endpoint[@name='NetTcpBinding_IEcommerceServiceContract']/@address"              :"netTcpBinding_IEcommerceServiceContract",

    },
	'Log4net.config':
	{
		"/configuration/log4net/appender[@name='SmtpAppender']/smtpHost/@value"                         :"SMTPHost",
		"/configuration/log4net/appender[@name='SmtpAppender']/to/@value"                            	:"SMTPTo",
		"/configuration/log4net/appender[@name='SmtpAppender']/from/@value"                           	:"SMTPFrom",
		"/configuration/log4net/appender[@name='SmtpAppender']/subject/@value"                          :"SMTPSubject",
	},
}

self.webfilelist['GooglePlaFeedGenerator']=self.DevGetFileList(base='src\\GooglePlaFeedGenerator\\bin\\$MODE',
                         xfiles=['buildinfo.xml',"*.resx","Default2.aspx","packages.config","*.targets","Web.Debug.Config","Web.Release.Config"])
self.webreplace['GooglePlaFeedGenerator']={
    'GooglePlaFeedGenerator.exe.config':
    {
		"/configuration/connectionStrings/add[@name='BatchDbConnectionString']/@connectionString"   	:"BatchDB",  
		"/configuration/connectionStrings/add[@name='SearchData.DbConnection']/@connectionString"   	:"OdysseyCommerceDB",  
		"/configuration/connectionStrings/add[@name='ProductData.DbConnection']/@connectionString"      :"ProductsDB",
		"/configuration/appSettings/add[@key='RunMode']/@value"											:"RunMode",
		"/configuration/appSettings/add[@key='MaximumThreadsToUse']/@value"								:"MaximumThreadsToUse",
		"/configuration/appSettings/add[@key='LimitTo100Products']/@value"								:"LimitTo100Products",
		"/configuration/appSettings/add[@key='SkipHasImageCheck']/@value"								:"SkipHasImageCheck",
		"/configuration/appSettings/add[@key='GzipFiles']/@value"										:"GzipFiles",
		"/configuration/appSettings/add[@key='OutputFolderPath']/@value"								:"OutputFolderPath",
		"/configuration/appSettings/add[@key='OutputFileMoveMethod']/@value"							:"OutputFileMoveMethod",
		"/configuration/appSettings/add[@key='FtpHost']/@value"											:"FtpHost",
		"/configuration/appSettings/add[@key='FtpDropFolderPath']/@value"								:"FtpDropFolderPath",
		"/configuration/appSettings/add[@key='FtpUserName']/@value"										:"FtpUserName",
		"/configuration/appSettings/add[@key='FtpUserPassword']/@value"									:"FtpUserPassword",
		"/configuration/appSettings/add[@key='FtpHostSecondary']/@value"								:"FtpHostSecondary",
		"/configuration/appSettings/add[@key='FtpDropFolderPathSecondary']/@value"						:"FtpDropFolderPathSecondary",
		"/configuration/appSettings/add[@key='FtpUserNameSecondary']/@value"							:"FtpUserNameSecondary",
		"/configuration/appSettings/add[@key='FtpUserPasswordSecondary']/@value"						:"FtpUserPasswordSecondary",
		"/configuration/appSettings/add[@key='TargetFolderPath']/@value"								:"TargetFolderPath",
		"/configuration/appSettings/add[@key='InputFolderPath']/@value"									:"InputFolderPath",
		"/configuration/appSettings/add[@key='AncillaryOutputFolderPath']/@value"						:"AncillaryOutputFolderPath",
		"/configuration/appSettings/add[@key='TargetFolderPathSecondary']/@value"						:"TargetFolderPathSecondary",
		"/configuration/appSettings/add[@key='AllowItemErrorsInFiles']/@value"							:"AllowItemErrorsInFiles",
		"/configuration/appSettings/add[@key='AllowDeletedCategories']/@value"							:"AllowDeletedCategories",
		"/configuration/appSettings/add[@key='AllowRuleOptimizations']/@value"							:"AllowRuleOptimizations",
		"/configuration/appSettings/add[@key='AllowRuleEntryRemovals']/@value"							:"AllowRuleEntryRemovals",
		"/configuration/appSettings/add[@key='AllowIEnumerableRuleEvaluations']/@value"					:"AllowIEnumerableRuleEvaluations",
    },
	'Log4net.config':
	{
		"/configuration/log4net/appender[@name='SmtpAppender']/smtpHost/@value"                         :"SMTPHost",
		"/configuration/log4net/appender[@name='SmtpAppender']/to/@value"                            	:"SMTPTo",
		"/configuration/log4net/appender[@name='SmtpAppender']/from/@value"                           	:"SMTPFrom",
		"/configuration/log4net/appender[@name='SmtpAppender']/subject/@value"                          :"SMTPSubject",
	},
}

self.webfilelist['IndigoToGoogleTaxonomyMapping']=self.DevGetFileList(base='src\\IndigoToGoogleTaxonomyMapping\\bin\\$MODE',
                         xfiles=['buildinfo.xml',"*.resx","Default2.aspx","packages.config","*.targets","Web.Debug.Config","Web.Release.Config"])
self.webreplace['IndigoToGoogleTaxonomyMapping']={
    'IndigoToGoogleTaxonomyMapping.exe.config':
    {
		"/configuration/connectionStrings/add[@name='BatchDbConnectionString']/@connectionString"   	:"BatchDB",  

		"/configuration/appSettings/add[@key='InputFileFolderPath']/@value"								:"InputFileFolderPath",
    },
}

self.webfilelist['NewGoogleCategoriesForIndigoCategoriesImporter']=self.DevGetFileList(base='src\\NewGoogleCategoriesForIndigoCategoriesImporter\\bin\\$MODE',
                         xfiles=['buildinfo.xml',"*.resx","Default2.aspx","packages.config","*.targets","Web.Debug.Config","Web.Release.Config"])
self.webreplace['NewGoogleCategoriesForIndigoCategoriesImporter']={
    'NewGoogleCategoriesForIndigoCategoriesImporter.exe.config':
    {
		"/configuration/connectionStrings/add[@name='BatchDbConnectionString']/@connectionString"   	:"BatchDB",  
		"/configuration/appSettings/add[@key='InputFileFolderPath']/@value"								:"InputFileFolderPath",
    },
	'Log4net.config':
	{
		"/configuration/log4net/appender[@name='SmtpAppender']/smtpHost/@value"                         :"SMTPHost",
		"/configuration/log4net/appender[@name='SmtpAppender']/to/@value"                            	:"SMTPTo",
		"/configuration/log4net/appender[@name='SmtpAppender']/from/@value"                           	:"SMTPFrom",
		"/configuration/log4net/appender[@name='SmtpAppender']/subject/@value"                          :"SMTPSubject",
	},
}

self.webfilelist['CjFeedGenerator']=self.DevGetFileList(base='src\\CjFeedGenerator\\bin\\$MODE',
                         xfiles=['buildinfo.xml',"*.resx","Default2.aspx","packages.config","*.targets","Web.Debug.Config","Web.Release.Config"])
self.webreplace['CjFeedGenerator']={
    'CjFeedGenerator.exe.config':
    {
		"/configuration/connectionStrings/add[@name='BatchDbConnectionString']/@connectionString"   	:"BatchDB",   
		"/configuration/connectionStrings/add[@name='SearchData.DbConnection']/@connectionString"   	:"OdysseyCommerceDB",  
		"/configuration/connectionStrings/add[@name='ProductData.DbConnection']/@connectionString"      :"ProductsDB", 
			
		"/configuration/appSettings/add[@key='FeedId']/@value"											:"FeedId",
		"/configuration/appSettings/add[@key='AllowIncrementalRuns']/@value"							:"AllowIncrementalRuns",
		"/configuration/appSettings/add[@key='AllowItemErrorsInFiles']/@value"							:"AllowItemErrorsInFiles",
		"/configuration/appSettings/add[@key='AllowDeletedCategories']/@value"							:"AllowDeletedCategories",
		"/configuration/appSettings/add[@key='AllowRuleOptimizations']/@value"							:"AllowRuleOptimizations",
		"/configuration/appSettings/add[@key='AllowRuleEntryRemovals']/@value"							:"AllowRuleEntryRemovals",
		"/configuration/appSettings/add[@key='AllowIEnumerableRuleEvaluations']/@value"					:"AllowIEnumerableRuleEvaluations",
		"/configuration/appSettings/add[@key='MaximumThreadsToUse']/@value"								:"MaximumThreadsToUse",
		"/configuration/appSettings/add[@key='LimitTo100Products']/@value"								:"LimitTo100Products",
		"/configuration/appSettings/add[@key='SkipHasImageCheck']/@value"								:"SkipHasImageCheck",
		"/configuration/appSettings/add[@key='TestIncrementalRunFromDate']/@value"						:"TestIncrementalRunFromDate",
		"/configuration/appSettings/add[@key='InputFolderPath']/@value"									:"InputFolderPath",
		"/configuration/appSettings/add[@key='AncillaryOutputFolderPath']/@value"						:"AncillaryOutputFolderPath",
		"/configuration/appSettings/add[@key='GzipFiles']/@value"										:"GzipFiles",
		"/configuration/appSettings/add[@key='OutputFolderPath']/@value"								:"OutputFolderPath",
		"/configuration/appSettings/add[@key='OutputFileMoveMethod']/@value"							:"OutputFileMoveMethod",
		"/configuration/appSettings/add[@key='FtpHost']/@value"											:"FtpHost",
		"/configuration/appSettings/add[@key='FtpDropFolderPath']/@value"								:"FtpDropFolderPath",
		"/configuration/appSettings/add[@key='FtpUserName']/@value"										:"FtpUserName",
		"/configuration/appSettings/add[@key='FtpUserPassword']/@value"									:"FtpUserPassword",
		"/configuration/appSettings/add[@key='TargetFolderPath']/@value"								:"TargetFolderPath",
    },
	'Log4net.config':
	{
		"/configuration/log4net/appender[@name='SmtpAppender']/smtpHost/@value"                         :"SMTPHost",
		"/configuration/log4net/appender[@name='SmtpAppender']/to/@value"                            	:"SMTPTo",
		"/configuration/log4net/appender[@name='SmtpAppender']/from/@value"                           	:"SMTPFrom",
		"/configuration/log4net/appender[@name='SmtpAppender']/subject/@value"                          :"SMTPSubject",
	},
}

self.webfilelist['BVRatingImporter']=self.DevGetFileList(base='src\\BVRatingImporter\\bin\\$MODE',
                         xfiles=['buildinfo.xml',"*.resx","Default2.aspx","packages.config","*.targets","Web.Debug.Config","Web.Release.Config"])
self.webreplace['BVRatingImporter']={
    'BVRatingImporter.exe.config':
    {
		"/configuration/connectionStrings/add[@name='BatchDbConnectionString']/@connectionString"   	:"BatchDB",  
		"/configuration/appSettings/add[@key='FtpHost']/@value"											:"FtpHost",
		"/configuration/appSettings/add[@key='NoFtpDownload']/@value"									:"NoFtpDownload",
		"/configuration/appSettings/add[@key='DownloadFolderPath']/@value"								:"DownloadFolderPath",
    },
	'Log4net.config':
	{
		"/configuration/log4net/appender[@name='SmtpAppender']/smtpHost/@value"                         :"SMTPHost",
		"/configuration/log4net/appender[@name='SmtpAppender']/to/@value"                            	:"SMTPTo",
		"/configuration/log4net/appender[@name='SmtpAppender']/from/@value"                           	:"SMTPFrom",
		"/configuration/log4net/appender[@name='SmtpAppender']/subject/@value"                          :"SMTPSubject",
	},
}

self.webfilelist['RRFeedGenerator']=self.DevGetFileList(base='src\\RRFeedGenerator\\bin\\$MODE',
                         xfiles=['buildinfo.xml',"*.resx","Default2.aspx","packages.config","*.targets","Web.Debug.Config","Web.Release.Config"])
self.webreplace['RRFeedGenerator']={
    'RRFeedGenerator.exe.config':
    {
		"/configuration/connectionStrings/add[@name='BatchDbConnectionString']/@connectionString"   	:"BatchDB",   
		"/configuration/connectionStrings/add[@name='SearchData.DbConnection']/@connectionString"   	:"OdysseyCommerceDB",  
		"/configuration/connectionStrings/add[@name='ProductData.DbConnection']/@connectionString"      :"ProductsDB", 
			
		"/configuration/appSettings/add[@key='LimitTo100Products']/@value"								:"LimitTo100Products",
		"/configuration/appSettings/add[@key='MaximumThreadsToUse']/@value"								:"MaximumThreadsToUse",
		"/configuration/appSettings/add[@key='SkipHasImageCheck']/@value"								:"SkipHasImageCheck",
		"/configuration/appSettings/add[@key='OutputFileMoveMethod']/@value"							:"OutputFileMoveMethod",
		"/configuration/appSettings/add[@key='FtpHost']/@value"											:"FtpHost",
		"/configuration/appSettings/add[@key='FtpDropFolderPath']/@value"								:"FtpDropFolderPath",
		"/configuration/appSettings/add[@key='FtpUserName']/@value"										:"FtpUserName",
		"/configuration/appSettings/add[@key='FtpUserPassword']/@value"									:"FtpUserPassword",
		"/configuration/appSettings/add[@key='ZipFiles']/@value"										:"ZipFiles",
		"/configuration/appSettings/add[@key='InputFolderPath']/@value"									:"InputFolderPath",
		"/configuration/appSettings/add[@key='OutputFolderPath']/@value"								:"OutputFolderPath",
		"/configuration/appSettings/add[@key='WorkingDirectory']/@value"								:"WorkingDirectory",
		"/configuration/appSettings/add[@key='ProductFilesPath']/@value"								:"ProductFilesPath",
		"/configuration/appSettings/add[@key='AttributeFilesPath']/@value"								:"AttributeFilesPath",
		"/configuration/appSettings/add[@key='ProductCategoryFilesPath']/@value"						:"ProductCategoryFilesPath",
		"/configuration/appSettings/add[@key='AllowIncrementalRuns']/@value"							:"AllowIncrementalRuns",

    },
	'Log4net.config':
	{
		"/configuration/log4net/appender[@name='SmtpAppender']/smtpHost/@value"                         :"SMTPHost",
		"/configuration/log4net/appender[@name='SmtpAppender']/to/@value"                            	:"SMTPTo",
		"/configuration/log4net/appender[@name='SmtpAppender']/from/@value"                           	:"SMTPFrom",
		"/configuration/log4net/appender[@name='SmtpAppender']/subject/@value"                          :"SMTPSubject",
	},
}

self.webfilelist['DynamicCampaignsFeedGenerator']=self.DevGetFileList(base='src\\DynamicCampaignsFeedGenerator\\bin\\$MODE',
                         xfiles=['buildinfo.xml',"*.resx","Default2.aspx","packages.config","*.targets","Web.Debug.Config","Web.Release.Config"])
self.webreplace['DynamicCampaignsFeedGenerator']={
    'DynamicCampaignsFeedGenerator.exe.config':
    {
		"/configuration/connectionStrings/add[@name='BatchDbConnectionString']/@connectionString"   	:"BatchDB",   
		"/configuration/connectionStrings/add[@name='SearchData.DbConnection']/@connectionString"   	:"OdysseyCommerceDB",  
		"/configuration/connectionStrings/add[@name='ProductData.DbConnection']/@connectionString"      :"ProductsDB", 
			
		"/configuration/appSettings/add[@key='LimitTo100Products']/@value"								:"LimitTo100Products",
		"/configuration/appSettings/add[@key='MaximumThreadsToUse']/@value"								:"MaximumThreadsToUse",
		"/configuration/appSettings/add[@key='GzipFiles']/@value"										:"GzipFiles",
		"/configuration/appSettings/add[@key='InputFolderPath']/@value"									:"InputFolderPath",
		"/configuration/appSettings/add[@key='OutputFolderPath']/@value"								:"OutputFolderPath",
		"/configuration/appSettings/add[@key='OutputFileMoveMethod']/@value"							:"OutputFileMoveMethod",
		"/configuration/appSettings/add[@key='FtpHost']/@value"											:"FtpHost",
		"/configuration/appSettings/add[@key='FtpDropFolderPath']/@value"								:"FtpDropFolderPath",
		"/configuration/appSettings/add[@key='FtpUserName']/@value"										:"FtpUserName",
		"/configuration/appSettings/add[@key='FtpUserPassword']/@value"									:"FtpUserPassword",
		"/configuration/appSettings/add[@key='SkipHasImageCheck']/@value"								:"SkipHasImageCheck",
		"/configuration/appSettings/add[@key='AllowItemErrorsInFiles']/@value"							:"AllowItemErrorsInFiles",
		"/configuration/appSettings/add[@key='AllowDeletedCategories']/@value"							:"AllowDeletedCategories",
		"/configuration/appSettings/add[@key='AllowRuleOptimizations']/@value"							:"AllowRuleOptimizations",
		"/configuration/appSettings/add[@key='AllowRuleEntryRemovals']/@value"							:"AllowRuleEntryRemovals",
		"/configuration/appSettings/add[@key='AllowIEnumerableRuleEvaluations']/@value"					:"AllowIEnumerableRuleEvaluations",
		"/configuration/appSettings/add[@key='BaseUrl']/@value"											:"BaseUrl",
    },
	'Log4net.config':
	{
		"/configuration/log4net/appender[@name='SmtpAppender']/smtpHost/@value"                         :"SMTPHost",
		"/configuration/log4net/appender[@name='SmtpAppender']/to/@value"                            	:"SMTPTo",
		"/configuration/log4net/appender[@name='SmtpAppender']/from/@value"                           	:"SMTPFrom",
		"/configuration/log4net/appender[@name='SmtpAppender']/subject/@value"                          :"SMTPSubject",
	},
}