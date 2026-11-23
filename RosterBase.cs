namespace CasualHockeySim;

using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Text.Json;

public record TeamImport(int Id, string Name, string City);
public record PlayerImport(int Id, string Name, int TeamId, int Overall);

public partial class RosterBase(string teamsFilePath = "Data/defaultTeams.json", string playersFilePath = "Data/defaultPlayers.json")
{
    private readonly string _teamsFilePath = string.IsNullOrWhiteSpace(teamsFilePath)
            ? "Data/defaultTeams.json"
            : teamsFilePath;

    private readonly string _playersFilePath = string.IsNullOrWhiteSpace(playersFilePath)
        ? "Data/defaultPlayers.json"
        : playersFilePath;
    public void Greet()
    {
        Console.WriteLine("Hello from RosterBase!");
    }
    public void buildRosters()
    {
        // making database schema
        using var connection = new SqliteConnection("Data Source=league.db");
        connection.Open();
        connection.Execute("PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;");

        connection.Execute("""
        CREATE TABLE IF NOT EXISTS Teams (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            City TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Players (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            TeamId INTEGER,
            Overall INTEGER,
            FOREIGN KEY (TeamId) REFERENCES Teams(Id)
        );

        CREATE INDEX IF NOT EXISTS idx_players_teamid ON Players(TeamId);
        """);
        if (!File.Exists(_teamsFilePath))
        {
            throw new FileNotFoundException($"Team File not Found at: {Path.GetFullPath(_teamsFilePath)}");
        }
        if (!File.Exists(_playersFilePath))
        {
            throw new FileNotFoundException($"Player File not Found at: {Path.GetFullPath(_playersFilePath)}");
        }
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        using FileStream teamStream = File.OpenRead(_teamsFilePath);
        var teams = JsonSerializer.Deserialize<List<TeamImport>>(teamStream, options) ?? [];

        using FileStream playerStream = File.OpenRead(_playersFilePath);
        var players = JsonSerializer.Deserialize<List<PlayerImport>>(playerStream, options) ?? [];

        using var transaction = connection.BeginTransaction();

        try
        {
            // insert teams
            connection.Execute("""
            INSERT INTO Teams (Id, Name, City) VALUES (@Id, @Name, @City)
            ON CONFLICT(Id) DO UPDATE SET Name = excluded.Name;
            """,
                teams,
                transaction
            );

            // insert players
            connection.Execute(
                "INSERT INTO Players (Name, TeamId, Overall) VALUES (@Name, @TeamId, @Overall);",
                players,
                transaction
            );

            transaction.Commit();
            Console.WriteLine($"Imported {teams.Count} teams and {players.Count} players.");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"Import failed: {ex.Message}");
        }
    }

}
