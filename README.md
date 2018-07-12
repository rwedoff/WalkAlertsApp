# WalkAlertsApp
Xamarin.Android Application for SAFER-SIM Experiment at UI

## How to Run
- Create a TCP Socket Connected on your computer or within Unity. Unity "the" project already has this build in. A good testing application can be found here: [http://sockettest.sourceforge.net/]
- Type in the TCP IP and Port into the app while being on the same Wifi network. This will not work on Eduroam or ATTWifi, so please use a hotspot or ClaraLab router.
- Press Connect and send a message to the phone. Listed below. For new messages, please modify the code, anything can be sent, it just matters on how the messages is processed.
  - PROHIBITIVEALERT;
    - This will light up the screen with a DO NOT CROSS message along with a sound and vibration.
  - PERMISSIVEALERT; 
    - This will light up the screen with a SAFE TO CROSS message along with a sound and vibration.
    
## How to Develop
This mobile application is a Xamarin.Android application. It is a cross between C# and Android if you are not familiar. To work on this application you need to install the Xamarin tools in Visual Studio. From there, it is pretty straight forward Android Development like normal, just using C# syntax instead of Java. 

### Summary of Code
- MainActivity.cs: This is the home screen of the application. Basically it tells the user short instructions on how to connect and the current Wifi network.
- ConnectToUnityActivty.cs: This activity handles the auto udp search feature, that is kind of flakey, and the input box of the IP and Port address for the main TCP connection.
- AlertActivity.cs: This activity handles all the alerts. It is a constant TCP connection that tells you if you are connected to the server and then it will receive messages and act according to the alert type.
- Assets folder: All the assets related to the project (audio files and non-exsistent icons)
- /Resources/layout: This is where the Android XMl layout files are. These are normal Android XML and you may follow Android documentation.
