/*****************************************************************************************************
* V1Staging Database Script
*
* Creates the migration V1Staging database, tables, and stored procedures.
*
* NOTES:
*	- varchar(max) is used for all Name, multi-relation, multi-longint, Text, and LongText fields.
*	- varbinary(max) is used for blob data (Attachments).
*	- All asset tables have AssetOID, AssetState, and AssetNumber fields.
*	- AssetOID is the primary key for all tables.
*	- For multi-relation fields values, multiple AssetOIDs are separated with semicolons.
*	
* HISTORY:
*	- 4/29/2013	AJB	Created initial version.	
*	- 5/1/2013	AJB Made all multi-relation and multi-longint fields varchar(max).
*	- 5/5/2013	AJB	Added NewAssetID field to all asset tables, also added spPurgeDatabase procedure.
*	- 5/9/2013	AJB Updated some fields to accept Null values, removed uneeded AssetNumber fields.
*	- 6/1/2013	AJB	Added table for standard list types.
*	- 6/3/2013	AJB	Changed [Order] columns to int datatype to ensure correct ORDER BY results.
*	- 6/5/2013	AJB	Changed all V1 Text fields to varchar(max).
*	- 6/14/2013	AJB Altered Asset columns in Links and Attachments tables to accept null values.
*	- 6/14/2013	AJB Changed name of the database to "V1Staging".
*	- 6/18/2013	AJB	Added BaseAssets to Conversations table. Used in 11.3 and earlier versions.
*	- 6/19/2013	AJB	Added migration statistics table.
*	- 7/15/2013	AJB	Added ImportStatus and ImportDetails fields to all asset tables.
*	- 7/15/2013	AJB Added import sprocs for Links, Conversations, and Attachments.
*	- 7/15/2013	AJB Removed unused fields from asset tables.
*	- 7/22/2013	AJB	Added script for getting Tasks for import.
*	- 8/17/2013	AJB	Added Parent column to Iterations table to support Rally iteration to project link.
*	- 8/18/2013	AJB	Added IsRelease column to Projects table to support Rally export. 
*	- 8/19/2013 AJB Added ParentType column to Tasks/Tests tables to support Rally export.
*	- 8/20/2013 AJB Added Password column to Members table.
*	- 8/22/2013	AJB Changed Tasks|Tests tables to allow nulls for Parent column.
*	- 8/22/2013 AJB Added TestSteps table to support export of Rally test steps.
*	- 8/22/2013 AJB Added RegressionTests table to support export of Rally regression tests.
*	- 8/22/2013 AJB Added BaseAssetType column to Conversations table to support Rally export.
*	- 8/23/2013 AJB Added Index column to Conversations table to support Rally export.
*	- 8/23/2013 AJB Added URL. AssetType columns to Attachments table to support Rally export.
*	- 9/9/2013	AKB	Added spGetAttachmentsForRallyImport stored procedure.
*	- 2/8/2014	MTB	Changed Stories.Parent to be varchar max
*	- 4/8/2015	MTB Changed Order to a long
*
*******************************************************************************************************/

USE master;
GO

/*****************************************************************************************************
* DATABASE
*******************************************************************************************************/

--Drop the database if it already exists.
IF EXISTS (SELECT name FROM master.dbo.sysdatabases WHERE name = N'V1StagingDisney')
BEGIN
	DROP DATABASE V1StagingDisney;
END

--Create the database.
CREATE DATABASE V1StagingDisney;
GO

--Set the recovery model.
--ALTER DATABASE V1StagingVillageHealth SET RECOVERY SIMPLE WITH NO_WAIT;
--GO

--Change data file initial size and file growth values
ALTER DATABASE V1StagingDisney MODIFY FILE 
(NAME = N'V1StagingDisney', SIZE = 100MB , FILEGROWTH = 25MB);
GO

--Change log file initial size and file growth values
ALTER DATABASE V1StagingDisney MODIFY FILE 
(NAME = N'V1StagingDisney_log', SIZE = 100MB , FILEGROWTH = 25MB);
GO

USE V1StagingDisney;
GO


/*****************************************************************************************************
* TABLES
*******************************************************************************************************/

--Migration statistics table.
CREATE TABLE MigrationStats (
	Name varchar(50) not null,						--Name of the statistic.
	Value varchar(max),								--Value of the statistic.
	Timestamp datetime								--Timestamp of the statistic.
);
GO

--Custom fields table.
CREATE TABLE CustomFields (
	AssetOID varchar(50) not null,					--V1 asset OID
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.
	FieldName varchar(max) not null,				--Name of the custom field like "Custom_Urgency"
	FieldType varchar(50) not null,					--Data type of the custom field like "Attribute|Relation"
	FieldValue varchar(max) 						--Value of the custom field
);
GO

--Standard list types table.
CREATE TABLE ListTypes (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	NewEpicAssetOID varchar(50),					--V1 asset OID assigned to the newly imported asset (for Epic conversion)
	AssetType varchar(50),							--V1 asset type
	AssetState varchar(50),							--V1 asset state
	Name varchar(max) not null,						--Text
	Description varchar(max)						--LongText
);
GO
	
--Members (Member) table.
CREATE TABLE Members (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	Email varchar(max),								--Text
	Nickname varchar(max) not null,					--Text
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	Phone varchar(max),								--Text
	DefaultRole varchar(50) not null,				--Relation to Role
	Username varchar(100),							--Text
	[Password] varchar(100),						--Text
	MemberLabels varchar(max),						--Multi-Relation to MemberLabel
	NotifyViaEmail varchar(50) not null,			--Boolean
	SendConversationEmails varchar(50) not null,	--Boolean
	CONSTRAINT [PK_Members] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--MemberGroups (MemberLabel) table.
CREATE TABLE MemberGroups (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.
	AssetState varchar(50),							--V1 asset state
	Name varchar(max) not null,						--Text
	Description varchar(max),						--LongText
	Members varchar(max),							--Multi-Relation to Member
	CONSTRAINT [PK_MemberGroups] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Projects (Scope) table.
CREATE TABLE Projects (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	Schedule varchar(50),							--Relation to Schedule
	Parent varchar(50),								--Relation to Scope
	IsRelease varchar(10),							--TRUE|FALSE
	Owner varchar(50),								--Relation to Member
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	EndDate varchar(50),							--Date
	BeginDate varchar(50) not null,					--Date
	Status varchar(50),								--Relation to ScopeStatus
	Members varchar(max),							--Multi-Relation to Member
	Scheme varchar(50),								--Relation to Scheme
	Reference varchar(max),							--Text
	CONSTRAINT [PK_Projects] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Projects (ScopeLabel) table.
CREATE TABLE Programs (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	Name varchar(max) not null,						--Text
	Description varchar(max),						--LongText
	Scopes varchar(max),							--Multi-Relation to Scope
	CONSTRAINT [PK_Programs] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Schedules (Schedule) table.
CREATE TABLE Schedules (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	TimeboxGap varchar(50) not null,				--Duration
	TimeboxLength varchar(50) not null,				--Duration
	Ideas varchar(max),								--Multi-LongInt
	MentionedInExpressions varchar(max),			--Multi-Relation to Expression
	CONSTRAINT [PK_Schedules] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Iterations (Timebox) table.
CREATE TABLE Iterations (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	State varchar(50),								--Iteration state
	Owner varchar(50),								--Relation to Member
	Parent varchar(50),								--Relation to Project (Rally)
	Schedule varchar(50),							--Relation to Schedule
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	TargetEstimate varchar(50),						--Numeric
	BeginDate varchar(50) not null,					--Date
	EndDate varchar(50) not null,					--Date
	CONSTRAINT [PK_Iterations] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--FeatureGroups (Theme) table.
CREATE TABLE FeatureGroups (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	AssetNumber varchar(50),						--V1 asset number
	NewAssetNumber varchar(50),						--V1 asset number
	Customer varchar(50),							--Relation to Member
	Owners varchar(max),							--Multi-Relation to Member
	Goals varchar(max),								--Multi-Relation to Goal
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	Reference varchar(max),							--Text
	[Order] bigint,									--Rank
	Value varchar(50),								--Numeric
	Estimate varchar(50),							--Numeric
	LastVersion varchar(max),						--Text
	Scope varchar(50) not null,						--Relation to Scope
	Risk varchar(50),								--Relation to WorkitemRisk
	Priority varchar(50),							--Relation to WorkitemPriority
	Status varchar(50),								--Relation to ThemeStatus
	Category varchar(50),							--Relation to ThemeCategory
	Source varchar(50),								--Relation to ThemeSource
	Parent varchar(50),								--Relation to Theme
	Area varchar(50),								--Relation to ThemeLabel
	CONSTRAINT [PK_FeatureGroups] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Issues (Issue) table.
CREATE TABLE Issues (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	AssetNumber varchar(50),						--V1 asset number
	NewAssetNumber varchar(50),						--V1 asset number
	Retrospectives varchar(max),					--Multi-Relation to Retrospective
	Team varchar(50),								--Relation to Team
	Scope varchar(50) not null,						--Relation to Scope
	Owner varchar(50),								--Relation to Member
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	IdentifiedBy varchar(max),						--Text
	Reference varchar(max),							--Text
	TargetDate varchar(50),							--Date
	Resolution varchar(max),						--LongText
	[Order] bigint,									--Rank
	ResolutionReason varchar(50),					--Relation to IssueResolution
	Source varchar(50),								--Relation to StorySource
	Priority varchar(50),							--Relation to IssuePriority
	Category varchar(50),							--Relation to IssueCategory
	Requests varchar(max),							--Multi-Relation to Request
	BlockedPrimaryWorkitems varchar(max),			--Multi-Relation to PrimaryWorkitem
	PrimaryWorkitems varchar(max),					--Multi-Relation to PrimaryWorkitem
	BlockedEpics varchar(max),						--Multi-Relation to Epic
	Epics varchar(max),								--Multi-Relation to Epic
	CONSTRAINT [PK_Issues] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Requests (Request) table.
CREATE TABLE Requests (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.
	AssetState varchar(50),							--V1 asset state
	AssetNumber varchar(50),						--V1 asset number
	NewAssetNumber varchar(50),						--V1 asset number
	Owner varchar(50),								--Relation to Member
	Scope varchar(50) not null,						--Relation to Scope
	Epics varchar(max),								--Multi-Relation to Epic
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	[Order] bigint,									--Rank
	Resolution varchar(max),						--LongText
	Reference varchar(max),							--Text
	RequestedBy varchar(max),						--Text
	ResolutionReason varchar(50),					--Relation to RequestResolution
	Source varchar(50),								--Relation to StorySource
	Priority varchar(50),							--Relation to RequestPriority
	Status varchar(50),								--Relation to RequestStatus
	Category varchar(50),							--Relation to RequestCategory
	CONSTRAINT [PK_Requests] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Goals (Goal) table.
CREATE TABLE Goals (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	AssetNumber varchar(50),						--V1 asset number
	NewAssetNumber varchar(50),						--V1 asset number
	TargetedBy varchar(max),						--Multi-Relation to Scope
	Scope varchar(50),								--Relation to Scope
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	Priority varchar(50),							--Relation to GoalPriority
	Category varchar(50),							--Relation to GoalCategory
	CONSTRAINT [PK_Goals] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Epics (Epic) table.
CREATE TABLE Epics (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	AssetNumber varchar(50),						--V1 asset number
	NewAssetNumber varchar(50),						--V1 asset number
	Owners varchar(max),							--Multi-Relation to Member
	Goals varchar(max),								--Multi-Relation to Goal
	Super varchar(50),								--Relation to Epic
	Risk varchar(50),								--Numeric
	Requests varchar(max),							--Multi-Relation to Request
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	Reference varchar(max),							--Text
	Scope varchar(50) not null,						--Relation to Scope
	Status varchar(50),								--Relation to EpicStatus
	Swag varchar(50),								--Numeric
	RequestedBy varchar(max),						--Text
	Value varchar(50),								--Numeric
	[Order] bigint,									--Rank
	BlockingIssues varchar(max),					--Multi-Relation to Issue
	Issues varchar(max),							--Multi-Relation to Issue
	Category varchar(50),							--Relation to EpicCategory
	Source varchar(50),								--Relation to StorySource
	Priority varchar(50),							--Relation to EpicPriority
	CONSTRAINT [PK_Epics] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Stories (Story) table.
CREATE TABLE Stories (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	AssetNumber varchar(50),						--V1 asset number
	NewAssetNumber varchar(50),						--V1 asset number
	SubState varchar(50),							--V1 asset sub-state (version 11 and below only)
	Timebox varchar(50),							--Relation to Timebox
	Customer varchar(50),							--Relation to Member
	Owners varchar(max),							--Multi-Relation to Member
	IdentifiedIn varchar(50),						--Relation to Retrospective
	Team varchar(50),								--Relation to Team
	Goals varchar(max),								--Multi-Relation to Goal
	AffectedByDefects varchar(max),					--Multi-Relation to Defect
	Super varchar(50),								--Relation to Epic
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	Reference varchar(max),							--Text
	ToDo varchar(50),								--Numeric
	DetailEstimate varchar(50),						--Numeric
	[Order] bigint,									--Rank
	Estimate varchar(50),							--Numeric
	LastVersion varchar(max),						--Text
	OriginalEstimate varchar(50),					--Numeric
	RequestedBy varchar(max),						--Text
	Value varchar(50),								--Numeric
	Scope varchar(50) not null,						--Relation to Scope
	Status varchar(50),								--Relation to StoryStatus
	Category varchar(50),							--Relation to StoryCategory
	Risk varchar(50),								--Relation to WorkitemRisk
	Source varchar(50),								--Relation to StorySource
	Priority varchar(50),							--Relation to WorkitemPriority
	Dependencies varchar(max),						--Multi-Relation to Story
	Dependants varchar(max),						--Multi-Relation to Story
	Parent varchar(max),							--Relation to Theme
	Requests varchar(max),							--Multi-Relation to Request
	BlockingIssues varchar(max),					--Multi-Relation to Issue
	Issues varchar(max),							--Multi-Relation to Issue
	Benefits varchar(max),							--LongText
	CONSTRAINT [PK_Stories] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Defects (Defect) table.
CREATE TABLE Defects (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	AssetNumber varchar(50),						--V1 asset number
	NewAssetNumber varchar(50),						--V1 asset number
	Timebox varchar(50),							--Relation to Timebox
	VerifiedBy varchar(50),							--Relation to Member
	Owners varchar(max),							--Multi-Relation to Member
	DuplicateOf varchar(50),						--Relation to Defect
	Team varchar(50),								--Relation to Team
	Versions varchar(max),							--Multi-Relation to VersionLabel
	Goals varchar(max),								--Multi-Relation to Goal
	AffectedPrimaryWorkitems varchar(max),			--Multi-Relation to PrimaryWorkitem
	AffectedByDefects varchar(max),					--Multi-Relation to Defect
	Super varchar(50),								--Relation to Epic
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	Reference varchar(max),							--Text
	ToDo varchar(50),								--Numeric
	DetailEstimate varchar(50),						--Numeric
	[Order] bigint,									--Rank
	Estimate varchar(50),							--Numeric
	FoundInBuild varchar(max),						--Text
	Environment varchar(max),						--Text
	Resolution varchar(max),						--LongText
	VersionAffected varchar(max),					--Text
	FixedInBuild varchar(max),						--Text
	FoundBy varchar(max),							--Text
	Scope varchar(50) not null,						--Relation to Scope
	Status varchar(50),								--Relation to StoryStatus
	Type varchar(50),								--Relation to DefectType
	ResolutionReason varchar(50),					--Relation to DefectResolution
	Source varchar(50),								--Relation to StorySource
	Priority varchar(50),							--Relation to WorkitemPriority
	Parent varchar(50),								--Relation to Theme
	Requests varchar(max),							--Multi-Relation to Request
	BlockingIssues varchar(max),					--Multi-Relation to Issue
	Issues varchar(max),							--Multi-Relation to Issue
	Dependencies varchar(max),						--Multi-Relation to Defect
	Dependants varchar(max),						--Multi-Relation to Defect
	CONSTRAINT [PK_Defects] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Tasks (Task) table.
CREATE TABLE Tasks (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	AssetNumber varchar(50),						--V1 asset number
	NewAssetNumber varchar(50),						--V1 asset number
	Customer varchar(50),							--Relation to Member
	Owners varchar(max),							--Multi-Relation to Member
	Goals varchar(max),								--Multi-Relation to Goal
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	Reference varchar(max),							--Text
	ToDo varchar(50),								--Numeric
	DetailEstimate varchar(50),						--Numeric
	[Order] bigint,									--Rank
	Estimate varchar(50),							--Numeric
	LastVersion varchar(max),						--Text
	Category varchar(50),							--Relation to TaskCategory
	Source varchar(50),								--Relation to TaskSource
	Status varchar(50),								--Relation to TaskStatus
	Parent varchar(50),								--Relation to PrimaryWorkitem
	ParentType varchar(50),							--Parent asset type
	CONSTRAINT [PK_Tasks] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--RegressionTests table.
CREATE TABLE RegressionTests (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	AssetNumber varchar(50),						--V1 asset number
	NewAssetNumber varchar(50),						--V1 asset number
	Owners varchar(max),							--Multi-Relation to Member
	Team varchar(50),								--Relation to Team
	Scope varchar(50),								--Relation to Scope
	Name varchar(max),								--Text
	Description varchar(max),						--LongText
	Reference varchar(max),							--Text
	Steps varchar(max),								--LongText
	Inputs varchar(max),							--LongText
	Setup varchar(max),								--LongText
	[Order] bigint,									--Rank
	ExpectedResults varchar(max),					--LongText
	Status varchar(50),								--Relation to TestStatus
	Category varchar(50),							--Relation to TestCategory
	GeneratedFrom varchar(50),						--Relation to RegressionTest
	RegressionSuites varchar(max),					--Multi-Relation to RegressionSuites
	Tags varchar(max)								--Text
	CONSTRAINT [PK_RegressionTests] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Tests (Test) table.
CREATE TABLE Tests (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	AssetNumber varchar(50),						--V1 asset number
	NewAssetNumber varchar(50),						--V1 asset number
	Owners varchar(max),							--Multi-Relation to Member
	Goals varchar(max),								--Multi-Relation to Goal
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	Reference varchar(max),							--Text
	ToDo varchar(50),								--Numeric
	DetailEstimate varchar(50),						--Numeric
	Steps varchar(max),								--LongText
	Inputs varchar(max),							--LongText
	Setup varchar(max),								--LongText
	[Order] bigint,									--Rank
	Estimate varchar(50),							--Numeric
	VersionTested varchar(max),						--Text
	ActualResults varchar(max),						--LongText
	ExpectedResults varchar(max),					--LongText
	Status varchar(50),								--Relation to TestStatus
	Category varchar(50),							--Relation to TestCategory
	Parent varchar(50),								--Relation to Workitem
	ParentType varchar(50),							--Parent asset type
	GeneratedFrom varchar(50),						--Relation to RegressionTest
	CONSTRAINT [PK_Tests] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--TestSteps (Test.Steps) table.
CREATE TABLE TestSteps (
	AssetOID varchar(50) not null,					--ObjectID
	TestCaseOID varchar(50),						--ObjectID of related test case
	ExpectedResult varchar(max),					--Expected result
	Input varchar(max),								--Input
	StepIndex int,									--Step index
	CreateDate varchar(50)							--Creation date
	CONSTRAINT [PK_TestSteps] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Links (Link) table.
CREATE TABLE Links (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	OnMenu varchar(50) not null,					--Boolean
	URL varchar(max) not null,						--Text
	Name varchar(max) not null,						--Text
	Asset varchar(50),								--Relation to BaseAsset
	CONSTRAINT [PK_Links] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Attachments (Attachment) table.
CREATE TABLE Attachments (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	Name varchar(max),								--Text
	Content varbinary(max),							--Blob
	ContentType varchar(max),						--Text
	FileName varchar(max),							--Text
	Description varchar(max),						--LongText
	Category varchar(50),							--Relation to AttachmentCategory
	Asset varchar(50),								--Relation to BaseAsset
	AssetType varchar(50),							--Type of base asset.
	URL varchar(max),								--Text
	CONSTRAINT [PK_Attachments] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Conversations (Expression) table.
CREATE TABLE Conversations (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	AuthoredAt varchar(50) not null,				--Date
	Content varchar(max) not null,					--Text
	Mentions varchar(max),							--Multi-Relation to Memebrs/BaseAsset
	BaseAssets varchar(max),						--Multi-Relation to BaseAsset (11.3 and above)
	BaseAssetType varchar(50),						--Base asset type
	Author varchar(50),								--Relation to Member
	Conversation varchar(50),						--Relation to Expression
	InReplyTo varchar(50),							--Relation to Expression
	[Index] int,									--Index number of conversation entry
	CONSTRAINT [PK_Conversations] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Actuals (Actual) table.
CREATE TABLE Actuals (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	Value varchar(50) not null,						--Numeric
	Date varchar(50) not null,						--Date
	Timebox varchar(50),							--Relation to Timebox
	Scope varchar(50),								--Relation to Scope
	Member varchar(50),								--Relation to Member
	Workitem varchar(50),							--Relation to Workitem
	Team varchar(50),								--Relation to Team
	CONSTRAINT [PK_Actuals] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO

--Teams (Team) table.
CREATE TABLE Teams (
	AssetOID varchar(50) not null,					--V1 asset OID
	NewAssetOID varchar(50),						--V1 asset OID assigned to the newly imported asset
	ImportStatus varchar(50),						--Status of the import (IMPORTED|SKIPPED|FAILED)
	ImportDetails varchar(max),						--Additional import details.	
	AssetState varchar(50),							--V1 asset state
	Description varchar(max),						--LongText
	Name varchar(max) not null,						--Text
	CapacityExcludedMembers varchar(max)			--Multi-Relation to Member
	CONSTRAINT [PK_Teams] PRIMARY KEY CLUSTERED ([AssetOID] ASC)
);
GO


/*****************************************************************************************************
* STORED PROCEDURES
*******************************************************************************************************/

-- PurgeDatabase stored procedure.
CREATE PROCEDURE spPurgeDatabase AS
	TRUNCATE TABLE ListTypes;
	TRUNCATE TABLE CustomFields;
	TRUNCATE TABLE Members;
	TRUNCATE TABLE MemberGroups;
	TRUNCATE TABLE Projects;
	TRUNCATE TABLE Programs;
	TRUNCATE TABLE Schedules;
	TRUNCATE TABLE Iterations;
	TRUNCATE TABLE FeatureGroups;
	TRUNCATE TABLE Issues;
	TRUNCATE TABLE Requests;
	TRUNCATE TABLE Goals;
	TRUNCATE TABLE Epics;
	TRUNCATE TABLE Stories;
	TRUNCATE TABLE Defects;
	TRUNCATE TABLE Tasks;
	TRUNCATE TABLE Tests;
	TRUNCATE TABLE RegressionTests;
	TRUNCATE TABLE TestSteps;
	TRUNCATE TABLE Links;
	TRUNCATE TABLE Attachments;
	TRUNCATE TABLE Conversations;
	TRUNCATE TABLE Actuals;
	TRUNCATE TABLE Teams;
GO

-- spGetAttachmentsForImport stored procedure.
CREATE PROCEDURE spGetAttachmentsForImport AS
BEGIN
	SET NOCOUNT ON;

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Members)
	AND Asset LIKE 'Member%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Teams)
	AND Asset LIKE 'Team%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Schedules)
	AND Asset LIKE 'Schedule%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Projects)
	AND Asset LIKE 'Scope%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Iterations)
	AND Asset LIKE 'Timebox%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Goals)
	AND Asset LIKE 'Goal%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM FeatureGroups)
	AND Asset LIKE 'Theme%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Requests)
	AND Asset LIKE 'Request%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Issues)
	AND Asset LIKE 'Issue%'

	UNION ALL

	-- NOTE: Currently for 11.3 and earlier (epic is story).
	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Epics)
	AND Asset LIKE 'Story%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Stories)
	AND Asset LIKE 'Story%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Defects)
	AND Asset LIKE 'Defect%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Tasks)
	AND Asset LIKE 'Task%'

	UNION ALL

	SELECT * FROM Attachments WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Tests)
	AND Asset LIKE 'Test%'

END
GO

-- spGetAttachmentsForRallyImport stored procedure.
CREATE PROCEDURE spGetAttachmentsForRallyImport AS
BEGIN
	SET NOCOUNT ON;

	SELECT b.NewAssetOID, a.* FROM Attachments AS A WITH (NOLOCK)
	INNER JOIN Epics AS b
	ON a.Asset = b.AssetOID
	
	UNION ALL
	
	SELECT b.NewAssetOID, a.* FROM Attachments AS A WITH (NOLOCK)
	INNER JOIN Stories AS b
	ON a.Asset = b.AssetOID

	UNION ALL

	SELECT b.NewAssetOID, a.* FROM Attachments AS A WITH (NOLOCK)
	INNER JOIN Defects AS b
	ON a.Asset = b.AssetOID

	UNION ALL

	SELECT b.NewAssetOID, a.* FROM Attachments AS A WITH (NOLOCK)
	INNER JOIN Tasks AS b
	ON a.Asset = b.AssetOID

	UNION ALL

	SELECT b.NewAssetOID, a.* FROM Attachments AS A WITH (NOLOCK)
	INNER JOIN Tests AS b
	ON a.Asset = b.AssetOID
	
	UNION ALL
	
	SELECT b.NewAssetOID, a.* FROM Attachments AS A WITH (NOLOCK)
	INNER JOIN RegressionTests AS b
	ON a.Asset = b.AssetOID

END
GO

-- spGetConversationsForImport stored procedure.
CREATE PROCEDURE spGetConversationsForImport AS
BEGIN
	SET NOCOUNT ON;

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Members)
	AND BaseAssets LIKE 'Member%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Teams)
	AND BaseAssets LIKE 'Team%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Schedules)
	AND BaseAssets LIKE 'Schedule%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Projects)
	AND BaseAssets LIKE 'Scope%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Programs)
	AND BaseAssets LIKE 'ScopeLabel%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Iterations)
	AND BaseAssets LIKE 'Timebox%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Goals)
	AND BaseAssets LIKE 'Goal%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM FeatureGroups)
	AND BaseAssets LIKE 'Theme%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Requests)
	AND BaseAssets LIKE 'Request%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Issues)
	AND BaseAssets LIKE 'Issue%'
	AND Author IS NOT NULL

	UNION ALL

	-- NOTE: Support for 11.3 and earlier (epic is story).
	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Epics)
	AND BaseAssets LIKE 'Story%'
	OR BaseAssets LIKE 'Epic%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Stories)
	AND BaseAssets LIKE 'Story%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Defects)
	AND BaseAssets LIKE 'Defect%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Tasks)
	AND BaseAssets LIKE 'Task%'
	AND Author IS NOT NULL

	UNION ALL

	SELECT * FROM Conversations WITH (NOLOCK)
	WHERE BaseAssets IN (SELECT AssetOID FROM Tests)
	AND BaseAssets LIKE 'Test%'
	AND Author IS NOT NULL

END
GO

-- spGetLinksForImport stored procedure.
CREATE PROCEDURE spGetLinksForImport AS
BEGIN
	SET NOCOUNT ON;

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Members)
	AND Asset LIKE 'Member%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Teams)
	AND Asset LIKE 'Team%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Schedules)
	AND Asset LIKE 'Schedule%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Projects)
	AND Asset LIKE 'Scope%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Iterations)
	AND Asset LIKE 'Timebox%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Goals)
	AND Asset LIKE 'Goal%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM FeatureGroups)
	AND Asset LIKE 'Theme%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Requests)
	AND Asset LIKE 'Request%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Issues)
	AND Asset LIKE 'Issue%'

	UNION ALL

	-- NOTE: Support for 11.3 and earlier (epic is story).
	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Epics)
	AND Asset LIKE 'Story%'
	OR Asset LIKE 'Epic%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Stories)
	AND Asset LIKE 'Story%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Defects)
	AND Asset LIKE 'Defect%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Tasks)
	AND Asset LIKE 'Task%'

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Tests)
	AND Asset LIKE 'Test%'

END
GO

-- spGetTasksForImport stored procedure.
CREATE PROCEDURE spGetTasksForImport AS
BEGIN
	SET NOCOUNT ON;

	SELECT 
		a.AssetOID, 
		a.AssetState, 
		a.AssetNumber,
		a.Name, 
		a.Description,
		a.Owners,
		a.[Order],
		a.Goals,
		a.Reference,
		a.DetailEstimate,
		a.ToDo,
		a.LastVersion,
		a.Estimate,
		b.NewAssetOID AS 'Category', 
		c.NewAssetOID AS 'Source', 
		d.NewAssetOID AS 'Status',
		e.NewAssetOID AS 'Parent'
	FROM Tasks AS a WITH (NOLOCK)
	LEFT OUTER JOIN ListTypes AS b ON a.Category = b.AssetOID
	LEFT OUTER JOIN ListTypes AS c ON a.Source = c.AssetOID
	LEFT OUTER JOIN ListTypes AS d ON a.Status = d.AssetOID
	LEFT OUTER JOIN Stories AS e ON a.Parent = e.AssetOID
	ORDER BY a.[Order] ASC;

END
GO

/****** Object:  StoredProcedure [dbo].[spGetLinksForJiraImport]    Script Date: 6/18/2014 2:40:44 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


-- spGetLinksForImport stored procedure.
CREATE PROCEDURE [dbo].[spGetLinksForJiraImport] AS
BEGIN
	SET NOCOUNT ON;

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Members)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Teams)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Schedules)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Projects)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Iterations)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Goals)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM FeatureGroups)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Requests)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Issues)

	UNION ALL

	-- NOTE: Support for 11.3 and earlier (epic is story).
	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Epics)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Stories)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Defects)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Tasks)

	UNION ALL

	SELECT * FROM Links WITH (NOLOCK)
	WHERE Asset IN (SELECT assetOID FROM Tests)

END

GO


