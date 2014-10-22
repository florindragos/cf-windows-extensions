USE [master]
GO

RESTORE DATABASE [<DatabaseName>]
FROM DISK = N'<NfsLocation>'
WITH REPLACE,
MOVE N'<DatabaseName>PriData' TO N'<RootDataPath>\MSSQL\DATA\<DatabaseName>PriData.mdf', 
MOVE N'<DatabaseName>Data01' TO N'<RootDataPath>\MSSQL\DATA\<DatabaseName>Data01.ndf',
MOVE N'<DatabaseName>Data02' TO N'<RootDataPath>\MSSQL\DATA\<DatabaseName>Data02.ndf',
MOVE N'<DatabaseName>LogData01' TO N'<RootDataPath>\MSSQL\LOG\<DatabaseName>Log01.ldf'
GO

ALTER DATABASE [<DatabaseName>] SET RECOVERY SIMPLE WITH NO_WAIT
GO

ALTER DATABASE [<DatabaseName>] SET COMPATIBILITY_LEVEL = 100
GO

IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [<DatabaseName>].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO

ALTER DATABASE [<DatabaseName>] SET ANSI_NULL_DEFAULT ON 
GO

ALTER DATABASE [<DatabaseName>] SET ANSI_NULLS ON 
GO

ALTER DATABASE [<DatabaseName>] SET ANSI_PADDING ON 
GO

ALTER DATABASE [<DatabaseName>] SET ANSI_WARNINGS ON 
GO

ALTER DATABASE [<DatabaseName>] SET ARITHABORT ON 
GO

ALTER DATABASE [<DatabaseName>] SET AUTO_CLOSE OFF 
GO

ALTER DATABASE [<DatabaseName>] SET AUTO_CREATE_STATISTICS ON 
GO

ALTER DATABASE [<DatabaseName>] SET AUTO_SHRINK OFF 
GO

ALTER DATABASE [<DatabaseName>] SET AUTO_UPDATE_STATISTICS ON 
GO

ALTER DATABASE [<DatabaseName>] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO

ALTER DATABASE [<DatabaseName>] SET CURSOR_DEFAULT  LOCAL 
GO

ALTER DATABASE [<DatabaseName>] SET CONCAT_NULL_YIELDS_NULL ON 
GO

ALTER DATABASE [<DatabaseName>] SET NUMERIC_ROUNDABORT OFF 
GO

ALTER DATABASE [<DatabaseName>] SET QUOTED_IDENTIFIER ON 
GO

ALTER DATABASE [<DatabaseName>] SET RECURSIVE_TRIGGERS OFF 
GO

ALTER DATABASE [<DatabaseName>] SET  DISABLE_BROKER 
GO

ALTER DATABASE [<DatabaseName>] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO

ALTER DATABASE [<DatabaseName>] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO

ALTER DATABASE [<DatabaseName>] SET TRUSTWORTHY OFF 
GO

ALTER DATABASE [<DatabaseName>] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO

ALTER DATABASE [<DatabaseName>] SET PARAMETERIZATION SIMPLE 
GO

ALTER DATABASE [<DatabaseName>] SET READ_COMMITTED_SNAPSHOT OFF 
GO

ALTER DATABASE [<DatabaseName>] SET HONOR_BROKER_PRIORITY OFF 
GO

ALTER DATABASE [<DatabaseName>] SET  READ_WRITE 
GO

ALTER DATABASE [<DatabaseName>] SET RECOVERY FULL 
GO

ALTER DATABASE [<DatabaseName>] SET  MULTI_USER 
GO

ALTER DATABASE [<DatabaseName>] SET PAGE_VERIFY NONE  
GO

ALTER DATABASE [<DatabaseName>] SET DB_CHAINING OFF 
GO

ALTER DATABASE [<DatabaseName>] SET CURSOR_CLOSE_ON_COMMIT ON
GO

ALTER DATABASE [<DatabaseName>] SET RECOVERY SIMPLE 
GO

ALTER DATABASE [<DatabaseName>] SET AUTO_CREATE_STATISTICS ON
GO

ALTER DATABASE [<DatabaseName>] SET AUTO_UPDATE_STATISTICS ON
GO

ALTER DATABASE [<DatabaseName>] SET TORN_PAGE_DETECTION ON
GO

ALTER DATABASE [<DatabaseName>] SET AUTO_SHRINK ON
GO