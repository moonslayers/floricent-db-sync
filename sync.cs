using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;


class SyncDb
{
    public void SyncTables(string sourceConnectionString, string targetConnectionString)
    {

        // Cambia a "admProductos" si es necesario

        using (SqlConnection sourceConnection = new SqlConnection(sourceConnectionString))
        using (SqlConnection targetConnection = new SqlConnection(targetConnectionString))
        {
            sourceConnection.Open();
            targetConnection.Open();

            List<string> tablas = new List<string>(){
                    "admClientes",
                    "admProductos",
                    "admMovimientos",
                    "admDocumentos",
                    "admUnidadesMedidaPeso",
                };

            if (CompareAndSyncTables(sourceConnection, targetConnection, "admProductos", "CIDPRODUCTO"))
            {
                //implementar logica para cuando se haya sincronizado
            }



            if (CompareAndSyncTables(sourceConnection, targetConnection, "admClientes", "CIDCLIENTEPROVEEDOR"))
            {
                //implementar logica para cuando se haya sincronizado
            }

            Console.WriteLine("Sincronización completada.");
        }
    }

    static bool CompareAndSyncTables(SqlConnection sourceConnection, SqlConnection targetConnection, string tableName, string id)
    {
        bool rowsToUpdate = false;
        // Consulta SQL para encontrar filas que están en una tabla pero no en la otra
        string selectQuery = $@"
        SELECT * FROM adCENTRO_FLORICULTOR_D.dbo.{tableName}
        WHERE {id} NOT IN (SELECT {id} FROM syncAux.dbo.{tableName})";


        using (SqlCommand command = new SqlCommand(selectQuery, sourceConnection))
        using (SqlDataAdapter adapter = new SqlDataAdapter(command))
        {
            DataTable dataTable = new DataTable();
            adapter.Fill(dataTable);

            // Inserta las filas encontradas en syncAux
            if (dataTable.Rows.Count > 0)
            {
                // Convierte las filas a JSON y agrégalas a la lista
                List<string> jsonRows = new List<string>();
                foreach (DataRow row in dataTable.Rows)
                {
                    var rowAsDictionary = new Dictionary<string, object>();

                    foreach (DataColumn column in dataTable.Columns)
                    {
                        rowAsDictionary[column.ColumnName] = row[column];
                    }

                    string jsonRow = JsonConvert.SerializeObject(rowAsDictionary);
                    jsonRows.Add(jsonRow);
                }
                SendDataToNodeAPI(tableName, jsonRows);

                rowsToUpdate = true; // Se encontraron filas para actualizar
                Console.WriteLine(tableName + " actualizada.");

                // Inserta las filas encontradas en syncAux
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(targetConnection))
                {
                    bulkCopy.DestinationTableName = tableName;
                    bulkCopy.WriteToServer(dataTable);
                }
            }
        }
        return rowsToUpdate;
    }

    static async void SendDataToNodeAPI(string tablename, List<string> jsonData)
    {
        // Combina todas las cadenas JSON en una sola cadena
        string combinedJsonData = "[" + string.Join(",", jsonData) + "]";
        using (var client = new HttpClient())
        {
            var requestData = new
            {
                tablename = tablename,
                jsonRows = jsonData // jsonData es la lista de registros en formato JSON
            };

            // Serializar el objeto en una cadena JSON
            string jsonRequest = JsonConvert.SerializeObject(requestData);

            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            // Define la URL a la que deseas enviar los datos
            string url = "http://localhost:3000/conpaq/add-rows";

            // Realiza la solicitud POST
            var response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Datos enviados a la API en Node.js con éxito.");
            }
            else
            {
                Console.WriteLine("Error al enviar los datos a la API en Node.js: ", response.StatusCode);
            }
        }
    }

}
