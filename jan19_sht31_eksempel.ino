/*
  SHT31 er Grove sin temperatur- og 
  fuktighetssensor. Vi bruker denne sammen
  med arduino nano 33 BLE for å printe ut
  temperatur-verdiene til Seriell Monitor
  én gang i sekundet. 
*/

#include <ArudinoBLE.h>
#include <Arduino.h>
#include "SHT31.h"

SHT31 sht31 = SHT31();

float old_temp = 0;

void setup() {
  // put your setup code here, to run once:
  Serial.begin(9600);
  while(!Serial);
  Serial.println("Begynn...");
  sht31.begin();

  if(!sht31.begin())
  {
    Serial.println("Failed to initialize 
    temperature sensor!");
    while(1);
  }
}

void loop() {
  // put your main code here, to run repeatedly:

  //Leser av sensor verdiene
  float temp = sht31.getTemperature();

  //Sjekker om endring i temperatur er større enn 0.5grader C.
  if(abs(old_temp - temp)) >= 0.5
  {
    old_temp = temp;
    //printer sensorverdiene
    Serial.print("Temp = ");
    Serial.print(temp);
    Serial.print("grader C"); //enheten til Celcius
    Serial.println();
  }
  //printer sensorverdiene
  Serial.print("Temp = ");
  Serial.print(temp);
  Serial.print("grader C"); //enheten til Celcius
  //printer en tom linje
  Serial.println();
  //venter 1 sekund før vi printer igjen
  delay(1000);
}
