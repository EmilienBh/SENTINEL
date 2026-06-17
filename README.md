# Unity_VR_Evtol_Navigation

## Description

This is the repository of a VR eVTOL Navigation project, developed with Unity. It demonstrates a manual or automatic flight of an assisted eVTOL, and mainly focuses on the conduct modes (the user being able to switch between command sets), and the screen interface (flight-assistance widgets, auto-pilot mode, and a training interface).

## RDI Method: EDC(s)

| EDCs| PDE | Internal Article |Current State| Main File| Main Contributor|Corresponding EDC|
|----------|----------|----------|----------|----------|----------|----------|
| 1.123| [Link to PDE](https://capgemini.sharepoint.com/:p:/r/sites/VIABLERIProject-ASO/Shared%20Documents/P%C3%B4le%20VIABLE%20Automated%20Systems%20and%20Avionics/TdQ%201.09%20Cockpit%20et%20IHM/EdC.VIAB.1.123%20-%20Comment%20faire%20pour%20rendre%20le%20cockpit%20ergonomique%20afin%20de%20r%C3%A9duire%20la%20charge%20mentale%20du%20pilote/EdC.VIAB.1.123-Sheet%20PDE-EN.pptx?d=w76f30670f13c4f42837527af84125b53&csf=1&web=1&e=1QHrZc)| [Link to Internal Article](https://capgemini.sharepoint.com/:w:/r/sites/VIABLERIProject-ASO/Shared%20Documents/P%C3%B4le%20VIABLE%20Automated%20Systems%20and%20Avionics/TdQ%201.09%20Cockpit%20et%20IHM/EdC.VIAB.1.123%20-%20Comment%20faire%20pour%20rendre%20le%20cockpit%20ergonomique%20afin%20de%20r%C3%A9duire%20la%20charge%20mentale%20du%20pilote/EdC.VIAB.1.123-Internal%20paper-EN.docx?d=w24e01cbc8c5d41fdbbce9fc63551377f&csf=1&web=1&e=FgT2PJ) | In Execution | [Link to Main File](https://capgemini.sharepoint.com/:f:/r/sites/VIABLERIProject-ASO/Shared%20Documents/P%C3%B4le%20VIABLE%20Automated%20Systems%20and%20Avionics/TdQ%201.09%20Cockpit%20et%20IHM/EdC.VIAB.1.123%20-%20Comment%20faire%20pour%20rendre%20le%20cockpit%20ergonomique%20afin%20de%20r%C3%A9duire%20la%20charge%20mentale%20du%20pilote?csf=1&web=1&e=sXKC72) | Alexis Marceau | X


This Readme has to be converted to a pdf file for each new EDC using the following terminology EDCNumber_Readme_V_Versionnumber.pdf and uploaded to the project SharePoint. It needs to be done when:
- a CIR review happened
- the new EDC is completed 

In the first case, the column "Main file" must be updated with the file name the main contributor is working on for the corresponding new EDCs ( corresponding EDCs line). The main contributor also needs to update the "Current State." The number of the current EDC and the name of the main contributor need to be completed. There is no need to complete "PDE" and "Internal Article" since, at this stage, no PDE and internal articles have been produced. The line of the corresponding EDC needs to be highlighted in red.

In the second case, the only difference from the previous cases is that the columns "PDE" and "Internal Article" need to be completed with the corresponding state, and "Current State" needs to be changed to Consolidated.

To add value to each part of the code produced in a project, they must be linked to an EDC.


## Requirements

To run this project, you need :
- A Unity installation :
    - Version 2021.3.44f1 (with a Unity Pro license)
    - With the Android build support (Unity module)
    - A code editor, preferably VS Community

To build the VR project, you need :
- A compatible VR headset (developed with an Oculus Quest 2)
- A PC with USB ports (if you are on a Capgemini PC, you need your ports to be unlocked)
- The software SideQuest, to build the APK on the headset
- Preferably an access to the Viable Google Drive, where to store and retreive APK versions of the app

## Installation

Once you have the appropriate Unity version instlaled on your PC, clone this repo and open it with Unity.
The first time you open the project, remember to switch it to Android build platform.

## Usage

If you want to update this project, see Documentation.
If you want to run or build this project :
- In the project scene, go to : <---- CODE ----> / <Container [drone mover variants]>. There, you have an OnEnableComponent, where you should always tick only one of the two booleans (to enable either the keyboard-mouse commands for a PC test, or the VR-controller commands for an APK build).
- If you test the project on PC (keyboard-mouse), there are the commands :
    - Head orientation : Mouse
    - Click : Space
    - "Left stick" : ZQSD
    - "Right stick" : Keyboard arrows

## Documentation
For developer documentation, see this document : 
[Developer documentation](https://capgemini.sharepoint.com/:w:/r/sites/VIABLERIProject-ASO/Shared%20Documents/P%C3%B4le%20VIABLE%20Automated%20Systems%20and%20Avionics/TdQ%201.09%20Cockpit%20et%20IHM/EdC.VIAB.1.123%20-%20Comment%20faire%20pour%20rendre%20le%20cockpit%20ergonomique%20afin%20de%20r%C3%A9duire%20la%20charge%20mentale%20du%20pilote/Developer_Guide-Unity_VR_Evtol_Navigation.docx?d=w2bfa6762970a43b491ebfb996c9d416a&csf=1&web=1&e=AFSiUb)

## Contributing
- Added several conduct modes and the possibility to switch between them at any time
- Added several widgets to the pilot interface, including a battery depletion system with emergency landing on low battery
- Added a circuit system, designed to have metrics to evaluate the performance of users based on selected conduct mode

## License
This code is for internal use and research purposes only.

## Contact
Alexis Marceau : alexis.marceau@capgemini.com
Roza Cherfi : roza.cherfi@capgemini.com

## Acknowledgements
See references, especially Lilium eVTOL, that have been as an inspiration base for the conduct modes of this simulation

## References
References used to conceive conduct modes are stored and commented in this document : 
[Perspectives de conduite et Scénarios de crise](https://capgemini.sharepoint.com/:w:/r/sites/VIABLERIProject-ASO/Shared%20Documents/P%C3%B4le%20VIABLE%20Automated%20Systems%20and%20Avionics/TdQ%201.09%20Cockpit%20et%20IHM/EdC.VIAB.1.107-Quelle%20strat%C3%A9gie%20SVO%20faut-il%20impl%C3%A9menter%20sur%20l%27eVTOL%20et%20comment%20am%C3%A9liorer%20l%27immersion%20du%20pilote/Perspectives%20de%20conduite%20et%20sc%C3%A9narios%20de%20crise.docx?d=w3b769abb56914142be62450c4da7cb0e&csf=1&web=1&e=buzfl6)

