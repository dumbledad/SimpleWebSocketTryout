//
//  ViewController.swift
//  WebSocketClient
//
//  Created by Tim Regan on 11/11/2018.
//  Copyright © 2018 Tim Regan. All rights reserved.
//

import UIKit
import Starscream

// https://github.com/daltoniam/Starscream
class ViewController: UIViewController {
    var socket: WebSocket?
    
    override func viewDidLoad() {
        super.viewDidLoad()
        setupSocket()
    }
    
    func setupSocket() {
        //socket = WebSocket(url: URL(string: "ws://192.168.1.148:8080")!)
        socket = WebSocket(url: URL(string: "ws://192.168.1.148:23949")!)
        socket?.delegate = self
        socket?.connect()
    }
}

extension ViewController: WebSocketDelegate {
    func websocketDidConnect(socket: WebSocketClient) {
        print("websocket is connected")
        socket.write(string: "Hallo server")
    }
    
    func websocketDidDisconnect(socket: WebSocketClient, error: Error?) {
        print("websocket is disconnected: \(error?.localizedDescription ?? "<no value>")")
    }
    
    func websocketDidReceiveMessage(socket: WebSocketClient, text: String) {
        print("got some text: \(text)")
    }
    
    func websocketDidReceiveData(socket: WebSocketClient, data: Data) {
        print("got some data: \(data.count)")
    }
}

