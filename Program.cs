using System;

namespace MiAplicacion
{
    class Program
    {
        static void Main()
        {
            //correr solo la primera vez para crear la base de datos auxiliar syncAux
            Db db = new Db();
            string connectionString = "Data Source=localhost;Initial Catalog=master;User ID=sa;Password=Soluciones@01";
            db.Conectar(connectionString);

            //correr para sincronizar cada cierto tiempo.
            SyncDb sync=new SyncDb();
            string source="Data Source=localhost;Initial Catalog=adCENTRO_FLORICULTOR_D;User ID=sa;Password=Soluciones@01";
            string target="Data Source=localhost;Initial Catalog=syncAux;User ID=sa;Password=Soluciones@01";
            sync.SyncTables(source,target);
        }
    }
}