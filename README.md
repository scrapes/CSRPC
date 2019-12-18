# CsRCP
A easy to use, UDP based C# RPC libary.

This libary does provide an easy way to make function calls on the remote machine using coustom parameters. 
This libary is experimental and not maintained. 

<a href="https://travis-ci.org/scrapes/rpc_net_lib/"><img src="https://api.travis-ci.org/scrapes/rpc_net_lib.svg?branch=master"></a>


Howto:

The Libary itself can be used with a premade Socket or only given IPEndPoint to connect to.
The Intention is to have an Server <-> Client scenario.


# 1. Function Stack
First of all you have to define a Function Stack where all callable functions are defined.

```
class FStack
{
    [SERVER]
    public static bool _Init(EndPoint clientEP)
    {
        Console.WriteLine(clientEP.ToString());
        return true;
    }
    public static int inc(int num)
    {
        Console.WriteLine("NumTest");
        //Console.WriteLine(ipe.ToString());
        return ++num;
    }
}
```

This Function Stack has to be the same on all Clients.
The name has to be unique!

The Init Function will be needed to handle all the Clients.

The Server tag is added for controlling the execution flow. 
Only if you have Authority you can execute Server functions. 


# 2. Server
Next you have to define the Server.
```
Socket serverSo = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
serverSo.Bind(new IPEndPoint(IPAddress.Any, 20888));

net_manager server = new net_manager(serverSo, typeof(FStack), true);
server.has_authority = true;
```


Now you have started a new listener thread, that handles all incoming Packets.

# 3. Client

For the client side you have one easy call.

```
net_manager client = new net_manager(new IPEndPoint(IPAddress.Any, 20888), typeof(FStack), true);
client.Init();
```

You define wich Server you want to use, the type of the FunctionStack and if you wand to Autostart the Thread.

# 4. Using The Libary


Using the Libary is as easy as creating the connections.
You can just do a Single line Call to execute and return Procedures.

```
int ret = (int)client.Execute("inc", new object[] { 5 });
```

The Execute function returns automatically the returned object from the remote execution.
The name of the function has to be a string, this is the reason the name has to be unique.
The calling parameters are bundled in a object array. 

For the Server Side you have to define the Client EndPoint as Well.

```
int ret = (int)server.Execute("inc", new object[] { 5 }, client1.ipe);
```

The handling and indexing of the client EndPoints has to be done manually.


# 5. Handling the Clients as Server

As mentioned above you can use the ```_Init``` function as  Handling function.

# 6. ??? ... Profit!

Use it as you like, Commercial or Free, as long as you name me as contributer.



