-- Create the database
CREATE DATABASE MissingPersonsDB;
GO

-- Use the new database
USE MissingPersonsDB;
GO

-- Create the MissingPersons table
CREATE TABLE MissingPersons (
    ID INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100),
    Race NVARCHAR(50),
    Age INT,
    Sex NVARCHAR(10),
    Height NVARCHAR(20),
    Weight NVARCHAR(20),
    EyeColor NVARCHAR(20),
    Hair NVARCHAR(50),
    Alias NVARCHAR(100),
    Tattoos NVARCHAR(MAX),
    LastSeen DATE,
    DateReported DATE,
    MissingFrom NVARCHAR(100),
    ConditionsOfDisappearance NVARCHAR(MAX),
    OfficerInfo NVARCHAR(100),
    PhoneNumber1 NVARCHAR(20),
    PhoneNumber2 NVARCHAR(20),
    CurrentStatus NVARCHAR(7) CHECK (CurrentStatus IN ('Missing', 'Found'))
);

ALTER TABLE MissingPersons
ADD Latitude DECIMAL(9,6),
    Longitude DECIMAL(9,6);

-- Add columns for DateFound and PdfName
ALTER TABLE MissingPersons
ADD DateFound DATE NULL,
    PdfName NVARCHAR(100) NULL

-- Create indexes
CREATE NONCLUSTERED INDEX IX_MissingPersons_Name_Status ON MissingPersons (Name, CurrentStatus);
CREATE NONCLUSTERED INDEX IX_MissingPersons_LastSeen_Status ON MissingPersons (LastSeen, CurrentStatus);
CREATE NONCLUSTERED INDEX IX_MissingPersons_DateReported_Status ON MissingPersons (DateReported, CurrentStatus);
CREATE NONCLUSTERED INDEX IX_MissingPersons_MissingFrom_Status ON MissingPersons (MissingFrom, CurrentStatus);
CREATE NONCLUSTERED INDEX IX_MissingPersons_CurrentStatus ON MissingPersons (CurrentStatus);
CREATE NONCLUSTERED INDEX IX_MissingPersons_Name_Age_DateReported ON MissingPersons (Name, Age, LastSeen, DateReported);

-- Insert sample data
INSERT INTO MissingPersons (
    Name, Race, Age, Sex, Height, Weight, EyeColor, Hair, Alias, Tattoos,
    LastSeen, DateReported, MissingFrom, ConditionsOfDisappearance, OfficerInfo,
    PhoneNumber1, PhoneNumber2, CurrentStatus
) VALUES (
    'John Doe', 'Caucasian', 35, 'Male', '5''10"', '180 lbs', 'Blue', 'Brown',
    'Johnny', 'Dragon on right arm', '2023-09-15', '2023-09-16', 'New York City, NY',
    'Last seen leaving work', 'Det. Jane Smith, Badge #12345', '555-123-4567', '555-987-6543',
    'Missing'
);

-- Verify the inserted data
SELECT * FROM MissingPersons;