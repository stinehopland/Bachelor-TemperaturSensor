/*
  SHT31 er Grove sin temperatur- og 
  fuktighetssensor. Vi bruker denne sammen
  med arduino nano 33 BLE for å printe ut
  temperatur-verdiene til Seriell Monitor
  én gang i sekundet. 
  Dette programmet skal skrive ut temperatur når man er koblet til arduino
  med bluetooth.
*/
#include <ArduinoBLE.h>
#include <SHT31.h>

float oldtemp = 0;
float newtemp = 0;

BLEService temperatureService("180A"); //BLE LED Service: Device information service

// BLE temperature Characteristic
BLEFloatCharacteristic temperatureCharacteristic("2A6E", BLERead);

SHT31 sht31 = SHT31();

void setup() {
  // put your setup code here, to run once:
  Serial.begin(9600);
  //while(!Serial);
  
  pinMode(LED_BUILTIN, OUTPUT);
  
  Serial.println("Begynn...");
  sht31.begin();
  
  if (!sht31.begin()){
    Serial.print("Temperaturesensor not connected");
  }
  else{
    Serial.println("Temperaturesensor connected");
  }
  
  // error message if Bluetooth does not begin
  if(!BLE.begin()){
    Serial.println("Starting BLE failed!");
    while(1);
  }

  // set advertised local name and service UUID:
  BLE.setLocalName("Nano 33 BLE");
  BLE.setAdvertisedService(temperatureService); 

  // add the characteristic of the service
  temperatureService.addCharacteristic(temperatureCharacteristic);

  // add service
  BLE.addService(temperatureService);

  // start advertising
  BLE.advertise();
  Serial.print("Pheripheral device Bluetooth Address: ");
  Serial.println(BLE.address());
  Serial.println("Waiting for connections..");

}

void loop() {
  // listen for BLE central to connect:
  BLEDevice central = BLE.central();
  
  long timestamp = 0;
  
  // if a central is connected to peripheral:
  if (central){
    Serial.println("Connected to central.");
    Serial.print("The clients MAC address is: ");
    // print the central's MAC address:
    Serial.println(central.address());
    Serial.print("Pheripheral device Bluetooth Address: ");
    Serial.println(BLE.address());
    digitalWrite(LED_BUILTIN, HIGH); // turn on LED to show it's connected
    
    // while central is connected to peripheral:
    while(central.connected()){
      newtemp = sht31.getTemperature();
      if(abs(newtemp - oldtemp)>1)
      {
        oldtemp = newtemp;
        Serial.print("Temp = ");
        Serial.print(newtemp);
        Serial.print(" grader C"); //enheten til Celcius
        //printer en tom linje
        Serial.println();
      }
      //venter 1 sekund før vi printer igjen
      temperatureCharacteristic.writeValue(newtemp);
    }

    // when the central disconnects:
    digitalWrite(LED_BUILTIN, LOW);
    Serial.print(F("Disconnected from central: "));
    Serial.println(central.address());    
  }
}