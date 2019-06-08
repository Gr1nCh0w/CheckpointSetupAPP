using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using Windows.Networking.Connectivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Newtonsoft.Json;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.UI.Popups;

namespace Checkpoint_Setup_APP
{
    public sealed partial class MainPage : Page
    {
        int PORT;
        string IPLocal, AdapterNamePI;
        bool ChangementEffectue = false;

        //--- Variable accés BDD ---
        string SQL_IP = "192.168.0.248";
        string SQL_DataBase = "CCT";
        string SQL_User = "sa";
        string SQL_Pswd = "sapassword";
        //-----------------------
        public MainPage()
        {
            this.InitializeComponent();

            TxtBx_IpSQL.Text = SQL_IP;
            GetLocalIp();
            GetPORT();
            
        }

        void GetPORT()
        {
            try
            {
                string String = string.Format("Server = {0}; Database = {1}; User Id = {2}; Password = {3}", SQL_IP, SQL_DataBase, SQL_User, SQL_Pswd);
                SqlConnection SQL_Cxn = new SqlConnection(String);

                string REQUETE = string.Format("SELECT NumPort FROM Table_Checkpoint WHERE IPCheckpoint = '{0}'", IPLocal);
                SqlCommand CMD = new SqlCommand();
                SqlDataReader READER;

                CMD.CommandText = REQUETE;
                CMD.CommandType = CommandType.Text;
                CMD.Connection = SQL_Cxn;

                SQL_Cxn.Open();                

                READER = CMD.ExecuteReader();

                while (READER.Read())
                {
                    if (!READER.IsDBNull(0))
                    {
                        TxtBx_PtAct.Text = READER.GetInt32(0).ToString();
                        PORT = READER.GetInt32(0);
                    }
                }

                SQL_Cxn.Close();
            }
            catch (SqlException)
            {
                MsgALERTAsync("Une erreur s'est produite lors de la récupération du PORT dans la BDD.\nVérifiez si l'adresse de la BDD est correct et si vous êtes bien sur le bon réseau.");
                throw;
            }
        }
        void GetLocalIp()
        {
            try
            {
                var icp = NetworkInformation.GetInternetConnectionProfile();

                if (icp?.NetworkAdapter == null)
                {
                    CoreApplication.Exit();
                }
                var hostname = NetworkInformation.GetHostNames().SingleOrDefault(hn => hn.IPInformation?.NetworkAdapter != null && hn.IPInformation.NetworkAdapter.NetworkAdapterId == icp.NetworkAdapter.NetworkAdapterId);

                TxtBx_IPAct.Text = hostname?.CanonicalName;
                IPLocal = hostname?.CanonicalName;
            }
            catch (Exception)
            {
                MsgALERTAsync("Une Erreur s'est produite lors de la récupération de l'IP.\nVérifiez si le Raspberry est bien branché au réseau.");
                throw;
            }            
        }
        void FindAdapterName()
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface adapter in nics)
            {
                if (adapter.Description == "LAN9512/LAN9514 USB 2.0 to Ethernet 10/100 Adapter")
                {
                    AdapterNamePI = adapter.Id;
                }
            }
        }
        async void ChangeIPAddress(string ipAddress)
        {
            if (TxtBx_IPChoix.Text != "")
            {
                if (TxtBx_IPChoix.Text != IPLocal)
                {
                    if (EntryVerificationIP() == true)
                    {
                        try
                        {
                            string String = string.Format("Server = {0}; Database = {1}; User Id = {2}; Password = {3}", SQL_IP, SQL_DataBase, SQL_User, SQL_Pswd);
                            SqlConnection SQL_Cxn = new SqlConnection(String);

                            string REQUETE = string.Format("UPDATE Table_Checkpoint SET IPCheckpoint = \'{0}\' WHERE IPCheckpoint = \'{1}\'", TxtBx_IPChoix.Text, IPLocal);
                            SqlCommand CMD = new SqlCommand();
                            SqlDataReader READER;

                            CMD.CommandText = REQUETE;
                            CMD.CommandType = CommandType.Text;
                            CMD.Connection = SQL_Cxn;

                            SQL_Cxn.Open();

                            READER = CMD.ExecuteReader();

                            while (READER.Read())
                            {
                                if (!READER.IsDBNull(0))
                                { }
                            }
                            SQL_Cxn.Close();

                            ChangementEffectue = true;
                        }
                        catch (SqlException)
                        {
                            MsgALERTAsync("Une erreur s'est produite lors du changement d'IP dans la BDD.\nVérifiez si l'adresse de la BDD est correct et si vous êtes bien sur le bon réseau.");
                            ChangementEffectue = false;
                            throw;
                        }

                        try
                        {
                            HttpClient client = new HttpClient();
                            client.BaseAddress = new System.Uri("http://127.0.0.1:8080/", UriKind.RelativeOrAbsolute);
                            client.DefaultRequestHeaders.Authorization =
                             new AuthenticationHeaderValue(
                             "Basic",
                             Convert.ToBase64String(
                             System.Text.ASCIIEncoding.ASCII.GetBytes(
                                 string.Format("{0}:{1}", "administrator", "admin"))));

                            NetworkConfiguration networkConfiguration = new NetworkConfiguration()
                            {
                                AdapterName = AdapterNamePI,
                                IPAddress = ipAddress,
                                SubnetMask = "255.255.255.0",
                                DefaultGateway = "192.168.0.254",
                                PrimaryDNS = "192.168.0.254",
                                SecondryDNS = "8.8.8.8"
                            };
                            string json = JsonConvert.SerializeObject(networkConfiguration);
                            HttpContent content = new StringContent(json);
                            var result = await client.PutAsync("api/networking/ipv4config", content);

                            ChangementEffectue = true;
                        }
                        catch (Exception)
                        {
                            ChangementEffectue = false;
                            throw;
                        }
                        
                    }
                    else
                    {
                        MsgALERTAsync("L'IP rentrée est incorrect.\nL'IP doit etre sous forme : 192.168.0.X\nAvec X compris entre 1 et 254.\n");
                        TxtBx_IPChoix.Text = "";
                        ChangementEffectue = false;
                    }
                }
                else
                {
                    MsgALERTAsync("L'IP rentrée est la même que celle de la RPI.");
                    TxtBx_IPChoix.Text = "";
                    ChangementEffectue = false;
                }
            }           
        }
        public class NetworkConfiguration
        {
            public string AdapterName { get; set; }
            public string IPAddress { get; set; }
            public string SubnetMask { get; set; }
            public string DefaultGateway { get; set; }
            public string PrimaryDNS { get; set; }
            public string SecondryDNS { get; set; }
        }
        void ChangePORT()
        {
            if (TxtBx_PtChoix.Text != "")
            {
                if (TxtBx_PtChoix.Text != TxtBx_PtAct.Text)
                {
                    if (EntryVerificationPORT() == true)
                    {
                        try
                        {
                            string PT = TxtBx_PtChoix.Text;

                            string String = string.Format("Server = {0}; Database = {1}; User Id = {2}; Password = {3}", SQL_IP, SQL_DataBase, SQL_User, SQL_Pswd);
                            SqlConnection SQL_Cxn = new SqlConnection(String);

                            string REQUETE = string.Format("UPDATE Table_Checkpoint SET NumPort = {0} WHERE IPCheckpoint = \'{1}\'", Convert.ToInt32(PT), IPLocal);
                            SqlCommand CMD = new SqlCommand();
                            SqlDataReader READER;

                            CMD.CommandText = REQUETE;
                            CMD.CommandType = CommandType.Text;
                            CMD.Connection = SQL_Cxn;

                            SQL_Cxn.Open();

                            READER = CMD.ExecuteReader();

                            while (READER.Read())
                            {
                                if (!READER.IsDBNull(0))
                                { }
                            }
                            SQL_Cxn.Close();

                            ChangementEffectue = true;
                        }
                        catch (SqlException)
                        {
                            MsgALERTAsync("Une erreur s'est produite lors de la connexion a la BDD.\nVérifiez si l'adresse de la BDD est correct et si vous êtes bien sur le bon réseau.");
                            ChangementEffectue = false;
                            throw;
                        }
                    }
                    else
                    {
                        TxtBx_IPChoix.Text = "";
                        MsgALERTAsync("PORT inutilisable..\nLes Ports utilisables sont compris entre 1024 et 49 151.");                        
                        ChangementEffectue = false;
                    }
                    
                }
                else
                {
                    TxtBx_IPChoix.Text = "";
                    MsgALERTAsync("Le PORT est le même que celui déjà attribué.");
                    ChangementEffectue = false;
                }
            }
        }
        private void Btn_SaveIP_Click(object sender, RoutedEventArgs e)
        {
            FindAdapterName();
            ChangePORT();
            ChangeIPAddress(TxtBx_IPChoix.Text);
            if (ChangementEffectue == true)
            {
                ShutdownManager.BeginShutdown(ShutdownKind.Restart, TimeSpan.FromSeconds(5));
                MsgALERTAsync("Le Raspberry va redémarrer dans 5 secondes.");
            }
            else
            {
                MsgALERTAsync("Aucunes modifications n'ont été éffectuées.\nPour Quitter, cliquez sur la croix en haut a droite.");
            }                     
        }
        private void EXIT_Click(object sender, RoutedEventArgs e)
        {
            CoreApplication.Exit();
        }
        async void MsgALERTAsync(string Message)
        {
            var messageDialog = new MessageDialog(Message);
            messageDialog.Title = "Informations";

            await messageDialog.ShowAsync();
        }
        private bool EntryVerificationIP()
        {
            bool Verif = false;
            string[] Octet = TxtBx_IPChoix.Text.Split('.');
            int NbSplit = Octet.Length;

            if (NbSplit != 4)
            {
                string Octet1 = String.Format("{0}", Octet);
                string Octet2 = String.Format("{1}", Octet);
                string Octet3 = String.Format("{2}", Octet);
                string Octet4 = String.Format("{3}", Octet);

                if (Convert.ToUInt32(Octet1) == 192)
                {
                    if (Convert.ToUInt32(Octet2) == 168)
                    {
                        if (Convert.ToUInt32(Octet3) == 0)
                        {
                            if (Convert.ToUInt32(Octet4) > 1 && Convert.ToUInt32(Octet4) < 254)
                            {
                                if (Convert.ToUInt32(Octet4) != 248)
                                {
                                    Verif = true;
                                }
                                else
                                {
                                    TxtBx_IPChoix.Text = "";
                                    Verif = false;
                                }                                    
                            }
                            else
                            {
                                TxtBx_IPChoix.Text = "";
                                Verif = false;
                            }
                        }
                        else
                        {
                            TxtBx_IPChoix.Text = "";
                            Verif = false;
                        }
                    }
                    else
                    {
                        TxtBx_IPChoix.Text = "";
                        Verif = false;
                    }
                }
                else
                {
                    TxtBx_IPChoix.Text = "";
                    Verif = false;
                }
            }
            return Verif;
        }
        private bool EntryVerificationPORT()
        {
            bool Verif = false;
            PORT = Convert.ToInt32(TxtBx_PtChoix.Text);
            
            if (PORT > 1024 && PORT < 49151)
            {
                Verif = true;
            }
            else
            {
                Verif = false;
                TxtBx_PtChoix.Text = "";
                MsgALERTAsync("PORT inutilisable..\nLes Ports utilisables sont compris entre 1024 et 49 151.");
            }
            return Verif;
        }

    }
}
