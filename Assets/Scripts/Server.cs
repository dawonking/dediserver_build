using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using UnityEngine;

public class Server
{
    public static int maxPlayers { get; private set; }
    public static int Port { get; private set; }   

    public static TcpListener _tcpListener;
    public static UdpClient _udpListener;

    public static Dictionary<int , Client> clients = new Dictionary<int, Client> ();
    public delegate void PacketHandler(int _fromClient, Packet _packet);
    public static Dictionary<int, PacketHandler> packetHandlers;

    
    /// <summary>
    /// server_set
    /// </summary>
    public static void Start(int player , int port)
    {
        maxPlayers = player;
        Port = port;

        //Initialize server data
        InitializeServerData();

        _tcpListener = new TcpListener(IPAddress.Any, Port);
        _tcpListener.Start();
        _tcpListener.BeginAcceptTcpClient(tcpConnectCallback, null);

        _udpListener = new UdpClient(Port);
        _udpListener.BeginReceive(udpReceiveCallback, null);

        Debug.Log($"Server start / port {Port} / RoomMax_Player = {maxPlayers}");


    }


    private static void tcpConnectCallback(IAsyncResult _result)
    {
        //EndAcceptTcpClient = ������ ����õ��� �񵿱������� �޾Ƶ��̰� ���� ȣ��Ʈ�����  ó���� �� Ŭ���̾�Ʈ ����
        TcpClient _client = _tcpListener.EndAcceptTcpClient(_result);
        _tcpListener.BeginAcceptTcpClient(tcpConnectCallback, null);
        Debug.Log($"connection from {_client.Client.RemoteEndPoint}");

        
        for(int i =1; i<= maxPlayers; i++)
        {
            if (clients[i].tcp.socket == null)
            {
                clients[i].tcp.Connect(_client);
                return;
            }
        }

        Debug.Log($"{_client.Client.RemoteEndPoint} failed to connect: Server full!");
    }

    public static void SendUDPData(IPEndPoint _clientEndPoint , Packet _packet)
    {
        try
        {
            if(_clientEndPoint != null)
            {
                //�����ͱ׷��� ���� ȣ��Ʈ�� �񵿱������� �����ϴ�. ����� ������ Connect�� ȣ���� �� �����˴ϴ�.
                //���� byte , ũ�� , �۾��Ϸ�� ���� �񵿱� �븮��, <-����
                _udpListener.BeginSend(_packet.ToArray(), _packet.Length(), _clientEndPoint, null, null);
            }
        }
        catch(Exception e)
        {
            Debug.Log($"Error sending data to {_clientEndPoint} via UDP: {e}");
        }
    }


    private static void udpReceiveCallback(IAsyncResult result)
    {
        try
        {
            IPEndPoint _clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            //EndReceive =�� �񵿱� �۾��� ���� ���� ���� �� ����� ���� �����͸� �����ϴ� IAsyncResult�Դϴ�.
            byte[] _data = _udpListener.EndReceive(result, ref _clientEndPoint);
            //�ݹ� ���
            _udpListener.BeginReceive(udpReceiveCallback, null);

            if (_data.Length < 4)
            {
                return;
            }

            using (Packet _packet = new Packet(_data))
            {
                int _clientId = _packet.ReadInt();

                if (_clientId == 0)
                {
                    return;
                }

                if (clients[_clientId].udp.endPoint == null)
                {
                    //���ο� �����϶� ����
                    clients[_clientId].udp.Connect(_clientEndPoint);
                    return;
                }

                //�̹� �����ҽ�
                //endpoint�� ��
                if (clients[_clientId].udp.endPoint.Equals(_clientEndPoint))
                {
                    //�߸��Ǵ� ���� ����
                    clients[_clientId].udp.HandleData(_packet);
                }
            }



        }
        catch(Exception e)
        {
            Debug.Log($"Error receiving UDP data: {e}");
        }
    }

    private static void InitializeServerData()
    {
        for (int i = 1; i <= maxPlayers; i++)
        {
            clients.Add(i, new Client(i));
        }

        //���� ���
        //ServerHandle���� ó���� ��Ŷ���� ���
        //packetHandlers�� delegate�� packetHandler�� ������ ���
        //ServerHandler�� int,Packet�� �޴´�.
        packetHandlers = new Dictionary<int, PacketHandler>()
        {
            { (int)ClientPackets.welcomeReceived, ServerHandle.WelcomeReceived },
            { (int)ClientPackets.playerMovement, ServerHandle.PlayerMovement },
            { (int)ClientPackets.playerShoot, ServerHandle.PlayerShoot },
            { (int)ClientPackets.playerThrowItem, ServerHandle.PlayerThrowItem }
        };

    }

    public static void Stop()
    {
        _tcpListener.Stop();
        _udpListener.Close();
    }


}
