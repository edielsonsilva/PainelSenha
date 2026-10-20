IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [AccessRole] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [Departments] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(80) NOT NULL,
    [Prefix] nvarchar(5) NOT NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Desks] (
    [Id] int NOT NULL IDENTITY,
    [DepartmentId] int NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_Desks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Desks_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Sequences] (
    [DepartmentId] int NOT NULL,
    [BusinessDate] date NOT NULL,
    [LastNumber] int NOT NULL,
    CONSTRAINT [PK_Sequences] PRIMARY KEY ([DepartmentId], [BusinessDate]),
    CONSTRAINT [FK_Sequences_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [UserDepartments] (
    [UserId] nvarchar(450) NOT NULL,
    [DepartmentId] int NOT NULL,
    CONSTRAINT [PK_UserDepartments] PRIMARY KEY ([UserId], [DepartmentId]),
    CONSTRAINT [FK_UserDepartments_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserDepartments_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Tickets] (
    [Id] bigint NOT NULL IDENTITY,
    [DepartmentId] int NOT NULL,
    [BusinessDate] date NOT NULL,
    [Number] int NOT NULL,
    [Prefix] nvarchar(max) NOT NULL,
    [IsPreferential] bit NOT NULL,
    [Status] int NOT NULL,
    [DeskId] int NULL,
    [IssuedAtUtc] datetime2 NOT NULL,
    [CalledAtUtc] datetime2 NULL,
    [ClosedAtUtc] datetime2 NULL,
    [OperatorId] nvarchar(max) NULL,
    [IssueKey] uniqueidentifier NOT NULL,
    [IssuedById] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Tickets_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Tickets_Desks_DeskId] FOREIGN KEY ([DeskId]) REFERENCES [Desks] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Events] (
    [Id] bigint NOT NULL IDENTITY,
    [TicketId] bigint NOT NULL,
    [Kind] int NOT NULL,
    [OccurredAtUtc] datetime2 NOT NULL,
    [OperatorId] nvarchar(max) NOT NULL,
    [DeskId] int NULL,
    [OperationKey] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Events] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Events_Desks_DeskId] FOREIGN KEY ([DeskId]) REFERENCES [Desks] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Events_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE UNIQUE INDEX [IX_Departments_Prefix] ON [Departments] ([Prefix]);

CREATE UNIQUE INDEX [IX_Desks_DepartmentId] ON [Desks] ([DepartmentId]);

CREATE INDEX [IX_Events_DeskId] ON [Events] ([DeskId]);

CREATE UNIQUE INDEX [IX_Events_OperationKey] ON [Events] ([OperationKey]);

CREATE INDEX [IX_Events_TicketId] ON [Events] ([TicketId]);

CREATE UNIQUE INDEX [IX_Tickets_DepartmentId_BusinessDate_Number] ON [Tickets] ([DepartmentId], [BusinessDate], [Number]);

CREATE INDEX [IX_Tickets_DepartmentId_Status_IsPreferential_BusinessDate_Number] ON [Tickets] ([DepartmentId], [Status], [IsPreferential], [BusinessDate], [Number]);

CREATE UNIQUE INDEX [IX_Tickets_DeskId] ON [Tickets] ([DeskId]) WHERE [Status] = 1;

CREATE UNIQUE INDEX [IX_Tickets_IssueKey] ON [Tickets] ([IssueKey]);

CREATE INDEX [IX_UserDepartments_DepartmentId] ON [UserDepartments] ([DepartmentId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261014212732_Inicial', N'10.0.13');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261015144519_DefineEsquemaPadraoDbo', N'10.0.13');

COMMIT;
GO

