# Sim2Real: Human Social Signal Detection

## Introduction

Sim2Real refers to techniques that can be used to transfer knowledge from one environment (e.g in simulation) to another (e.g. real world). While Sim2Real has been applied to various fields such as object recognition, human pose estimation etc. it has never been applied to recognizing human social signals. We want to deliver a high accuracy machine learning model to our supervisor Angelica Lim (angelica@sfu.ca) that can accurately recognize the following 3 facial expressions: Angry, Crying and Happy.

## Synthetic Data Generation
![total](https://github.com/softMonkeys/Sim2Real/blob/master/Images/total1.PNG)
To generate the synthetic data, go to open `UnitySimulationCode` by Unity version `2020.1.12f1`. Once the project is created and loaded, you will be presented with the Unity Editor interface. From the top menu bar, open _**Window**_ -> _**Package Manager**_. Click on the _**+**_ sign at the top-left corner of the _**Package Manager**_ window and then choose the option _**Add package from git URL...**_. Enter the address `com.unity.perception` and click _**Add**_. After these operations you shouldn't have any issue for running the code. Now, open _**Hierarchy**_ -> _**TutorialScene**_ -> _**Simulation Scenario**_. Under _**Inspector**_ -> _**ForegroundObjectPlacementRandomizer**_ GUI you should find the _**Prefabs**_ list, this is where you put different human models for generating data. The current avaiable models are:
* **Angry**: `black_female_angry_Pivot` `black_male_angry_Pivot` `old_caicasian_female_angry_Pivot` `old_caicasian_male_angry_Pivot` `young_asian_female_angry_Pivot` `young_asian_male_angry_Pivot`
* **Happy**: `black_female_happy_Pivot` `black_male_happy_Pivot` `old_caicasian_female_happy_Pivot` `old_caicasian_male_happy_Pivot` `young_asian_female_happy_Pivot` `young_asian_male_happy_Pivot`
* **Crying**: `black_female_happy_Pivot` `black_male_happy_Pivot` `old_caicasian_female_happy_Pivot` `old_caicasian_male_happy_Pivot` `young_asian_female_happy_Pivot` `young_asian_male_happy_Pivot`
If you do not have Unity Simulation member, you can still generate the data by using your local machine. Under _**Simulation Scenario**_ ->  _**Inspector**_ ->  _**Fixed Length Scenario**_ ->  _**Constants**_ ->  _**Total Iterations**_ has a default value of 50. It means it will generate 50 images after we clicked the  _**Play**_ button on top of the Unity editior. Change this value based on your requirement. You can find the path for the result under _**Console**_
![data](https://github.com/softMonkeys/Sim2Real/blob/master/Images/datas.PNG)
