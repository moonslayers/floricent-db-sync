using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.InteropServices.JavaScript;

class Db
{
    public void Conectar(string connectionString)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {//dd
            try
            {
                connection.Open();

                // Verificar si la base de datos syncAux existe y crearla si no existe.
                string createDatabaseQuery = "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'syncAux') CREATE DATABASE syncAux";
                using (SqlCommand createDatabaseCommand = new SqlCommand(createDatabaseQuery, connection))
                {
                    createDatabaseCommand.ExecuteNonQuery();
                }

                // Cambiar al contexto de la base de datos syncAux.
                //connection.ChangeDatabase("syncAux");
                //connection.ChangeDatabase("adCENTRO_FLORICULTOR_D");
                connection.ChangeDatabase("adEmpresa_pruebaFloricent");

                List<string> tablas = new List<string>(){
                    "admClientes",
                    "admProductos",
                    "admMovimientos",
                    "admDocumentos",
                    "admUnidadesMedidaPeso",
                    "admMonedas",
                };
                List<string> condiciones = new List<string>(){
                    "WHERE CESTATUS=1 AND (CTIPOCLIENTE=2 OR CTIPOCLIENTE=3)",
                    "WHERE CSTATUSPRODUCTO=1",
                    "FROM (SELECT m.*, ROW_NUMBER() OVER (PARTITION BY m.CIDPRODUCTO ORDER BY m.CFECHA DESC) AS row_number FROM admMovimientos m) AS temp WHERE temp.row_number = 1",                  
                    "WHERE CIDDOCUMENTODE=19",
                    "",
                    "",
                };

                for (int i = 0; i < tablas.Count; i++)
                {
                    if (!TableExists(connection, tablas[i]) && false)
                    {
                        CopyTableStructureAndData(connection, "adCENTRO_FLORICULTOR_D", tablas[i]);
                        Console.WriteLine("tabla "+tablas[i]+" creada en syncAux");
                    }
                    SendDataToNodeAPI(connection, tablas[i], condiciones[i]);
                }
                Console.WriteLine("base de datos creada con éxito.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }

    static bool TableExists(SqlConnection connection, string tableName)
    {
        string query = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'";
        using (SqlCommand command = new SqlCommand(query, connection))
        {
            int count = (int)command.ExecuteScalar();
            return count > 0;
        }
    }

    static void CopyTableStructureAndData(SqlConnection connection, string sourceDatabase, string tableName)
    {
        // Obtener la estructura de la tabla desde la base de datos fuente.
        string createTableQuery = $"SELECT * INTO {tableName} FROM {sourceDatabase}.dbo.{tableName} WHERE 1 = 0";
        using (SqlCommand createTableCommand = new SqlCommand(createTableQuery, connection))
        {
            createTableCommand.ExecuteNonQuery();
        }

        // Insertar todos los datos desde la base de datos fuente.
        string insertDataQuery = $"INSERT INTO {tableName} SELECT * FROM {sourceDatabase}.dbo.{tableName}";
        using (SqlCommand insertDataCommand = new SqlCommand(insertDataQuery, connection))
        {
            insertDataCommand.ExecuteNonQuery();
        }
    }
    static async void SendDataToNodeAPI(SqlConnection connection, string tablename, string condicion)
    {
        List<string> jsonData = GetAllRecordsAsJson(connection, tablename, condicion);
        // Combina todas las cadenas JSON en una sola cadena
        string combinedJsonData = "[" + string.Join(",", jsonData) + "]";
        using (var client = new HttpClient())
        {
            var requestData = new
            {
                tablename = tablename,
                jsonData = jsonData // jsonData es la lista de registros en formato JSON
            };

            // Serializar el objeto en una cadena JSON
            string jsonRequest = JsonConvert.SerializeObject(requestData);

            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            // Define la URL a la que deseas enviar los datos
            //puerto 3000 es para pruebas
            //string url = "http://3.132.141.153:3000/conpaq/db";
            //string url = "http://localhost:3000/conpaq/db";
            //mi maquina
            string url = "http://192.168.10.125:3000/conpaq/db";

            // Realiza la solicitud POST
            var response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Datos enviados a la API en Node.js con éxito.");
            }
            else
            {
                Console.WriteLine("Error al enviar los datos a la API en Node.js.");
            }
        }
    }
    static List<string> GetAllRecordsAsJson(SqlConnection connection, string tableName, string condicion)
    {
        List<string> jsonRecords = new List<string>();
        string selectQuery = $"SELECT * FROM {tableName} {condicion}";
        if(tableName=="admMovimientos"){
            selectQuery = $"SELECT * {condicion}";
        }
        //Console.WriteLine(tableName);
        

        using (SqlCommand command = new SqlCommand(selectQuery, connection))
        using (SqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                // Convierte cada fila en un objeto JSON y agrégalo a la lista
                jsonRecords.Add(DataRecordToJson(reader));
            }
        }

        return jsonRecords;
    }
    static string DataRecordToJson(SqlDataReader dataReader)
    {
        var jsonObject = new Dictionary<string, object>();

        for (int i = 0; i < dataReader.FieldCount; i++)
        {
            string columnName = dataReader.GetName(i);
            object columnValue = dataReader[i];

            jsonObject[columnName] = columnValue;
        }

        return JsonConvert.SerializeObject(jsonObject);
    }
}
