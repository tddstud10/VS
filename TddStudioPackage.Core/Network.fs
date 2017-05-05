module R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Network

open System.Net
open System.Net.Sockets

let freeTcpPort() = 
    let l = TcpListener(IPAddress.Loopback, 0)
    l.Start()
    let port = (l.LocalEndpoint :?> IPEndPoint).Port
    l.Stop()
    port