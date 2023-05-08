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
        //EndAcceptTcpClient = 들어오는 연결시도를 비동기적으로 받아들이고 원격 호스트통신을  처리할 새 클라이언트 생성
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
                //데이터그램을 원격 호스트에 비동기적으로 보냅니다. 대상은 이전에 Connect를 호출할 때 지정됩니다.
                //보낼 byte , 크기 , 작업완료시 보낼 비동기 대리자, <-전달
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
            //EndReceive =이 비동기 작업에 대한 상태 정보 및 사용자 정의 데이터를 저장하는 IAsyncResult입니다.
            byte[] _data = _udpListener.EndReceive(result, ref _clientEndPoint);
            //콜백 등록
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
                    //새로운 연결일때 연결
                    clients[_clientId].udp.Connect(_clientEndPoint);
                    return;
                }

                //이미 존재할시
                //endpoint로 비교
                if (clients[_clientId].udp.endPoint.Equals(_clientEndPoint))
                {
                    //잘못되는 전송 방지
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

        //사전 등록
        //ServerHandle에서 처리할 패킷들을 등록
        //packetHandlers는 delegate인 packetHandler를 가지고 등록
        //ServerHandler는 int,Packet을 받는다.
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
