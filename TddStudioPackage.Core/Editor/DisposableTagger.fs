﻿namespace R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Core.Editor

open System

type DisposableTagger() as x = 
    let logger = R4nd0mApps.TddStud10.Logger.LoggerFactory.logger
    let disposed : bool ref = ref false
    // TODO: PARTHO: This causes InvalidOperationException. Figure out why finalize is being called without 
    // object being fully initialized first.
    // Additional information: The initialization of an object or value resulted in an object or 
    // value being accessed recursively before it was fully initialized
    //override x.Finalize() = x.Dispose(false)
    abstract Dispose : bool -> unit
    
    override __.Dispose(disposing) = 
        logger.logInfof "Disposing tagger %A..." <| x.GetType().Name
        if not disposed.Value then 
            if disposing then 
                // Dispose managed resources.
                ()
            // Unmanaged resources here.
            ()
            disposed := true
    
    interface IDisposable with
        member x.Dispose() = 
            x.Dispose(true)
            GC.SuppressFinalize(x)
