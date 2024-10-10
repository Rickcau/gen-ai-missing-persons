using System;
using System.Data;
using System.Data.SqlClient;
using System.Text.Json;
using System.Threading.Tasks;
using blog_trigger_mp_ingest.Models;

namespace blog_trigger_mp_ingest.Helpers
{
    public class SQLMissingPersonHelper
    {
        private readonly string _connectionString;

        public SQLMissingPersonHelper(string connectionString)
        {
            _connectionString = connectionString;
        }

        // public async Task InsertMissingPersonAsync(string jsonData)
        public async Task InsertMissingPersonAsync(MissingPerson person)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // var person = JsonSerializer.Deserialize<MissingPerson>(jsonData);

            const string sql = @"
                INSERT INTO MissingPersons (
                    Name, Race, Age, Sex, Height, Weight, EyeColor, Hair, Alias, Tattoos,
                    LastSeen, DateReported, MissingFrom, ConditionsOfDisappearance, OfficerInfo,
                    PhoneNumber1, PhoneNumber2, CurrentStatus, Latitude, Longitude, PdfName
                ) VALUES (
                    @Name, @Race, @Age, @Sex, @Height, @Weight, @EyeColor, @Hair, @Alias, @Tattoos,
                    @LastSeen, @DateReported, @MissingFrom, @ConditionsOfDisappearance, @OfficerInfo,
                    @PhoneNumber1, @PhoneNumber2, @CurrentStatus, @Latitude, @Longitude, @PdfName
                )";

            using var command = new SqlCommand(sql, connection);

            command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = person.Name ?? (object)DBNull.Value;
            command.Parameters.Add("@Race", SqlDbType.NVarChar, 50).Value = person.Race ?? (object)DBNull.Value;
            command.Parameters.Add("@Age", SqlDbType.Int).Value = person.Age != 0 ? person.Age : (object)DBNull.Value;
            command.Parameters.Add("@Sex", SqlDbType.NVarChar, 10).Value = person.Sex ?? (object)DBNull.Value;
            command.Parameters.Add("@Height", SqlDbType.NVarChar, 20).Value = person.Height ?? (object)DBNull.Value;
            command.Parameters.Add("@Weight", SqlDbType.NVarChar, 20).Value = person.Weight ?? (object)DBNull.Value;
            command.Parameters.Add("@EyeColor", SqlDbType.NVarChar, 20).Value = person.EyeColor ?? (object)DBNull.Value;
            command.Parameters.Add("@Hair", SqlDbType.NVarChar, 50).Value = person.Hair ?? (object)DBNull.Value;
            command.Parameters.Add("@Alias", SqlDbType.NVarChar, 100).Value = person.Alias ?? (object)DBNull.Value;
            command.Parameters.Add("@Tattoos", SqlDbType.NVarChar).Value = person.Tattoos ?? (object)DBNull.Value;
            command.Parameters.Add("@LastSeen", SqlDbType.Date).Value = !string.IsNullOrEmpty(person.LastSeen) ? DateTime.Parse(person.LastSeen) : (object)DBNull.Value;
            command.Parameters.Add("@DateReported", SqlDbType.Date).Value = !string.IsNullOrEmpty(person.DateReported) ? DateTime.Parse(person.DateReported) : (object)DBNull.Value;
            command.Parameters.Add("@MissingFrom", SqlDbType.NVarChar, 100).Value = person.MissingFrom ?? (object)DBNull.Value;
            command.Parameters.Add("@ConditionsOfDisappearance", SqlDbType.NVarChar).Value = person.ConditionsOfDisappearance ?? (object)DBNull.Value;
            command.Parameters.Add("@OfficerInfo", SqlDbType.NVarChar, 100).Value = person.OfficerInfo ?? (object)DBNull.Value;
            command.Parameters.Add("@PhoneNumber1", SqlDbType.NVarChar, 20).Value = person.PhoneNumber1 ?? (object)DBNull.Value;
            command.Parameters.Add("@PhoneNumber2", SqlDbType.NVarChar, 20).Value = person.PhoneNumber2 ?? (object)DBNull.Value;
            command.Parameters.Add("@CurrentStatus", SqlDbType.NVarChar, 7).Value = "Missing" ?? (object)DBNull.Value;
            command.Parameters.Add("@Latitude", SqlDbType.Float).Value = person.Latitude ?? (object)DBNull.Value;
            command.Parameters.Add("@Longitude", SqlDbType.Float).Value = person.Longitude ?? (object)DBNull.Value;
            command.Parameters.Add("@PdfName", SqlDbType.NVarChar, 100).Value = person.PdfName ?? (object)DBNull.Value;

            await command.ExecuteNonQueryAsync();
        }
    }
}
