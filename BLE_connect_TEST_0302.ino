/*
Arduino Nano 33 BLE 
Kode tatt fra https://www.okdo.com/getting-started/get-started-with-arduino-nano-33-ble/
Bli kjent med BLE peripheral 
*/

#include <ArduinoBLE.h>

static const char* greeting = "Hello World";

BLEService greetingService("180C");

BLEStringCharacteristic greetingCharacteristic("2A56", BLERead, 13);

void setup() {
  // put your setup code here, to run once:
  Serial.begin(9600);
  while(!Serial);

  pinMode(LED_BUILTIN, OUTPUT);

  if(!BLE.begin()){
    Serial.println("starting BLE failed!");
    while(1);
  }

  BLE.setLocalName("Nano33BLE");  // Set name for connection
  BLE.setAdvertisedService(greetingService); // Advertise service
  greetingService.addCharacteristic(greetingCharacteristic); // Add characteristic to service
  BLE.addService(greetingService); // Add service
  greetingCharacteristic.setValue(greeting); // Set greeting string

  BLE.advertise();  // Start advertising
  Serial.print("Peripheral device MAC: ");
  Serial.println(BLE.address());
  Serial.println("Waiting for connections...");

}

void loop() {
  // put your main code here, to run repeatedly:

    BLEDevice central = BLE.central();  // Wait for a BLE central to connect

  // if a central is connected to the peripheral:
  if (central) {
    Serial.print("Connected to central MAC: ");
    // print the central's BT address:
    Serial.println(central.address());
    // turn on the LED to indicate the connection:
    digitalWrite(LED_BUILTIN, HIGH);

    while (central.connected()){} // keep looping while connected
    
    // when the central disconnects, turn off the LED:
    digitalWrite(LED_BUILTIN, LOW);
    Serial.print("Disconnected from central MAC: ");
    Serial.println(central.address());
  }

}
