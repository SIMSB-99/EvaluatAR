# Artifact Appendix (Required for all badges)
Paper title: **EvaluatAR: A Cross-Device Evaluation Framework for Rapid Prototyping of Bystander PETs in AR**

Requested Badge(s):
  - [x] **Available**
  - [x] **Functional**
  - [] **Reproduced**

## Description (Required for all badges)
Title= EvaluatAR: A Cross-Device Evaluation Framework for Rapid Prototyping of Bystander PETs in AR
Authors= Syed Ibrahim Mustafa Shah Bukhari, Matthew Corbett, Bo Ji, and Brendan David-John 
Year= 2026

Description: This artifact accompanies our paper, which presents EvaluatAR, a cross-device framework for early-stage evaluation and rapid prototyping of visual bystander privacy-enhancing technologies (PETs) for Augmented Reality (AR) headsets. The core artifact is a modular Unity/C# implementation, `EvaluatAR.cs`, that provides data-collection and replay hooks for integrating Unity-based PETs with target AR headsets. EvaluatAR supports elapsed time-based synchronized replay of PET input streams and logs PET outputs and performance measurements during replay, enabling controlled comparison across devices and PET configurations.

**NOTE: AR HEADSETS ARE NEEDED TO DEPLOY AND RUN THE DATA COLLECTION PART OF THIS ARTIFACT.**. The artifact also includes two reference PET instantiations used in the paper: a BystandAR-based explicit PET and a Cardea-inspired implicit PET. These implementations demonstrate how EvaluatAR can be applied across different PET designs and across the AR headsets evaluated in the paper: Magic Leap 2, HoloLens 2, and Meta Quest 3. As described in the paper, the Cardea-inspired PET was evaluated on Magic Leap 2 and Meta Quest 3, but not on HoloLens 2.

Finally, the artifact includes the analysis notebooks used to process the data collected through EvaluatAR and generate the quantitative results reported in the paper.

### Security/Privacy Issues and Ethical Concerns (Required for all badges)
This artifact does not contain malware, exploits, vulnerable services, or code that intentionally disables operating-system or network security mechanisms. Running the code within the provided codebase does not require elevated privileges.

The Unity components in this artifact are designed for AR headset-based PET evaluation and may access camera, pose, and other sensor streams when deployed on physical AR headsets. These streams can capture people, bystanders, and private environments. Researchers who reuse the framework to collect new data should therefore do so only in controlled settings, obtain appropriate consent from participants and any recorded bystanders, and follow their institution’s ethical review/IRB requirements. As stated within the Ethical Considerations section of the paper, we do not redistrubute the visual stimuli used in our experiments or framework generated logs to avoid risksing the privacy and personally identifiable information of the recorded individuals. 

It is important to note that the Unity version (2022.3.12f1) that we have used to develop EvaluatAR contains a known vulnerability: “CWE-426: Untrusted Search Path” (CVE-2025-59489). This results in a warning displayed for this project in the Unity Hub. This warning currently appears on all Unity projects with versions prior to Unity 6, after Unity 6 was released. However, Unity has confirmed that there is no evidence of active exploitation or impact of this vulneraibility on the users. After the artifact approval process, we will explore upgrading our codebase to Unity 6 in the same repository. We will add appropriate release tags to the reviewed artifacts and the Unity 6 updated version, allowing future users to access an updated version of the codebase while preserving the reviewed artifact version that was awarded the functional badge.

## Basic Requirements (Required for Functional and Reproduced badges)

### Hardware Requirements (Required for Functional and Reproduced badges)
Minimum hardware requirements:
PC: The PC should be able to run Unity 2022.3.12f1. The online available minimum requirements to run the specified version of the Unity editor are as follows:
- Operating system (OS): 64-bit Windows 10/11 or Linux  
- Processor: X64 CPU architecture with SSE2 instruction set support (Intel Core i5 or AMD equivalent)
- Memory (RAM): 16 GB
- Graphics API: DX10, DX11, or DX12 capable dedicated graphics card (NVIDIA or AMD)
- Storage: Atleast 40 GB

AR headsets: Any standalone AR headset that provides sensor data access for all sensors required for the PET being evaluated. **NOTE: AR HEADSETS ARE NEEDED TO DEPLOY AND RUN THE DATA COLLECTION PART OF THIS ARTIFACT.**

Specification of hardware used in our experiments:
PC: We used a PC with the following specifications:
- Name: Alienware Aurora R15
- OS: Windows 11 Pro
- Processor: 13th Gen Intel Core i9-13900KF
- RAM: 64 GB
- Grapohics: Nvidia GeForce RTX 3090 with 24 GB dedicated VRAM
- Storage: 3 TB

AR Headsets: We used the following three headsets for our experiments:
1. Magic Leap 2 (256 GB storage)
2. HoloLens 2 (4 GB RAM, 64 GB storage)
3. Meta Quest 3 (512 GB) with Quest Link cable
---------

### Software Requirements (Required for Functional and Reproduced badges)
- OS: Windows 11 Pro
- Unity Hub with Unity 2022.3.12f1. It should be installed with Android Build Support in Unity, including SDK/NDK tools, for Magic Leap 2 and Meta Quest 3 builds and Universal Windows Platform Build Support in Unity for HoloLens 2 builds
- OpenCV for Unity from Unity Asset Store
- Visual Studio 2022 with C# support
- Developer-mode enabled on all target headsets
- Required python packages for each PET's analysis can be found within the `Analysis.ipynb` file in each PET's folder
- You would require any stock (Abode Stock or ShutterStock) or self-recorded (consented) visual stimuli to replay on a monitor for data collection with the framework
- Python version 3.9.20 is used for the analysis notebooks

### Estimated Time and Storage Consumption (Required for Functional and Reproduced badges)
Cloning the GitHub repository and reviewing the core EvaluatAR implementation should require only a few minutes and less than 947 MB of disk space. Opening the artifact in Unity and configuring the relevant project dependencies requires additional setup time, primarily because Unity, headset SDKs, and platform-specific build modules must be installed separately. We estimate that installing Unity and the required platform build support takes approximately 3 to 4 hours and requires roughly 50 to 60 GB of disk space.

For a user who already has Unity 2022.3.12f1 and the relevant headset SDKs installed, importing or inspecting the EvaluatAR code and reference PET integrations should take approximately 15–30 minutes. Building and deploying a configured Unity project to one headset typically takes approximately 15 to 20 minutes per device, with additional time required for device pairing, permissions, and debugging. Each generated build and associated Unity project files may require several hundred MBs to a few GBs of storage.

## Environment (Required for all badges)

### Accessibility (Required for all badges)
https://github.com/SIMSB-99/EvaluatAR/tree/main

### Set up the environment (Required for Functional and Reproduced badges)
Use the following command to download the artifact codebase:
```bash
git clone https://github.com/SIMSB-99/EvaluatAR.git
cd EvaluatAR
```

The artifact was tested with Unity 2022.3.12f1. We recommend installing this Unity version through Unity Hub and adding the platform build modules required for the target headset: Android Build Support for Magic Leap 2 and Meta Quest 3, and Universal Windows Platform Build Support for HoloLens 2. You would then be able to open the instantiated PETs' codebase for the headset you have available. Then you may follow the instructions in the Github's readme file to add the framework implementation to any of the instantiated PETs's codebase for your target headset. You may then build the apk and develop it on device.

For the analysis code, follow the steps below set up the local environment:

#### Step 1 - Clone the Repository

```bash
git clone https://github.com/SIMSB-99/EvaluatAR.git
cd EvaluatAR
```

#### Step 2 - Set Up And Activiate A Local Environment

##### Option A: Using Anaconda 

```bash
conda create --name project-env python=3.9.20 -y
conda activate project-env
```

##### Option B: Using Python venv

```bash
# Windows
python -m venv venv
# (Command Prompt)
venv\Scripts\activate.bat
# (PowerShell)
.\venv\Scripts\Activate.ps1

# macOS/Linux
python3 -m venv venv
source venv/bin/activate
```

#### Step 3 - Install Dependencies
```bash
pip install -r requirements.txt
```

#### Step 4 - Run the **`Analysis.ipynb`** file (VS Code or Jupyter Notebook)

### Testing the Environment (Required for Functional and Reproduced badges)
To test the Unity environment, open Unity Hub and add/open the relevant instantiated PET project for the headset you have available with the specified Unity version. After opening the project, allow Unity to import the project files and compile the C# scripts. The environment is set up correctly if the project opens without missing-package errors, EvaluatAR.cs compiles successfully, and the relevant scene can be opened in the Unity Editor. There should be no error log in Unity's console.

As a basic functionality check, follow the repository README instructions to attach or verify the EvaluatAR.cs script within the target PET scene, assign the required scene objects in the Unity Inspector, and select the desired framework mode. The setup is successful if the script exposes its expected fields in the Inspector and the Unity Console does not report unresolved script, namespace, or package errors.

For a headset-specific check, enable developer mode on the target headset, connect the headset to the PC, and create a development build for the corresponding platform. For Magic Leap 2 and Meta Quest 3, build the project as an Android APK. For HoloLens 2, build the project using the Universal Windows Platform build target. The environment is set up correctly if the project builds successfully and can be deployed to the target headset. After launching the application on device, EvaluatAR should start in the selected mode and should be able to access the configured scene objects and device permissions required by the PET.

To test the Python analysis environment, open the **`Analysis.ipynb`** as per the instructions provided above. We have included sample data (representing the logs generated by EvaluatAR for each experiment, and sample privacy-safe visual stimuli for Case Studies 1 and 3). The analysis file is configured to use relative paths for this sample data. Hence, the code should run without any issues if the environment has been set up correctly. The testing steps above verify that the software environment, Unity codebase, headset build setup, and notebook dependencies are configured correctly.

## Artifact Evaluation (Required for Functional and Reproduced badges)

### Main Results and Claims

#### Main Result 1: Cross-headset performance evaluation of a PET
Our paper claims that EvaluatAR enables reproducible cross-headset performance evaluation of the same PET under standardized replay. This claim is reproducible by executing Experiment 1A and Experiment 1B within Case Study 1. In Experiment 1A, we vary the inference sampling interval from 0 to 8 and show that FPS increases across all three headsets, with ML2 achieving the highest FPS, followed by MQ3 and HL2; in Experiment 1B, we vary candidate bystander load from 1 to 12 faces and show headset-specific FPS degradation trends. We report these results in Figure 4 and Figure 5 of our paper.

#### Main Result 2: Generalizability across implicit and explicit PET designs
Our paper claims that EvaluatAR’s record-replay workflow generalizes across implicit and explicit PET design categories using PET-specific output measures. This claim is reproducible by executing Experiment 2. In this experiment, we vary headset, model stack configuration, and candidate bystander load, and show that ML2 achieves better runtime performance than MQ3, the low-precision stack does not reduce end-to-end processing time, and intent-to-enforcement correctness degrades under more demanding configurations. We report these results in Figure 6, Figure 7, Figure 9, and Figure 10 of our paper.

#### Main Result 3: Replay-based debugging and validation of PET modifications
Our paper claims that EvaluatAR supports rapid PET debugging by making privacy-relevant edge cases replayable under identical inputs. This claim is reproducible by executing Experiment 3. In this experiment, we vary the edge-case stimulus and BystandAR association logic, and show that the baseline fails frequently under overlapping and crossing faces, while Kalman Predicted Positioning achieves 10/10 passes across all tested edge cases. We report these results in Figure 8 and Table 2 of our paper.

### Experiments
The experiments below correspond to the three case studies reported in the paper. Each experiment uses EvaluatAR’s record-replay workflow: first, the relevant visual stimulus is replayed externally while EvaluatAR records the PET input streams in Collect mode; then, the recorded input data is loaded onto the target headset and replayed in Replay mode while the PET outputs and performance metrics are logged. The resulting CSV logs can then be analyzed using the corresponding `Analysis.ipynb` notebook in the PET folder.

#### Experiment 1A: PET configuration knob
- Time: approximately 30 human-minutes per PET per headset

This experiment reproduces Main Result 1. In this experiment, we instantiate EvaluatAR with BystandAR and vary the inference sampling interval parameter in the Unity editor across {0, 1, 2, 4, 8} on HoloLens 2, Magic Leap 2, and Meta Quest 3. The dependent variable is average FPS.

To run the experiment, open the BystandAR-based Unity project for the target headset and configure EvaluatAR in Replay mode using the corresponding recorded input data. For each headset, replay a single-person visual stimuli. For each video, run the PET with each inference sampling interval.

After collecting the replay logs (stored on each device), copy them to a PC and open `Analysis.ipynb`, set the input paths to the newly generated logs, and run the notebook cells for Experiment 1A. The analysis should generate the average-FPS summaries corresponding to Figure 4 in the paper. The expected trend is that larger inference sampling intervals increase FPS across all three headsets. ML2 should achieve the highest FPS, followed by MQ3 and then HL2. In the paper, this experiment used three videos and tested all interval values across the three headsets, resulting in 75 trials.

#### Experiment 1B: Candidate bystander load
Time: approximately 60 human-minutes

This experiment also reproduces Main Result 1. In this experiment, we instantiate EvaluatAR with BystandAR and vary candidate bystander load across {1, 2, 3, 4, 5, 7, 8, 10, 12} visible faces on HoloLens 2, Magic Leap 2, and Meta Quest 3. The dependent variable is average FPS.

To run the experiment, use the BystandAR-based Unity project and replay the segmented candidate-load stimulus on each headset. The stimulus should contain sequential scenes with the target face-count levels, separated by blank screens for segmentation. Use the per-headset inference sampling intervals selected from Experiment 1A: interval 8 for HL2, interval 4 for MQ3, and interval 2 for ML2, as stated in our paper.

After collecting the replay logs, open `Analysis.ipynb`, set the input paths to the newly generated logs, and run the notebook cells for Experiment 1B. The analysis should generate the candidate-load FPS summary corresponding to Figure 5 in the paper. The expected result is that the headsets differ both in baseline FPS and in how performance changes as candidate load increases: ML2 has the highest average FPS, followed by MQ3 and HL2, while MQ3 shows the steepest FPS decline as load increases. In the paper, this experiment replayed one segmented candidate-load video once on each headset, resulting in three trials.

#### Experiment 2: Model stack configuration and candidate load
- Time: approximately 20 human-minutes per PET per headset

This experiment reproduces Main Result 2. In this experiment, we instantiate EvaluatAR with the Cardea-inspired explicit PET and vary headset, model stack configuration, and candidate bystander load. The model stack has two levels, high and low, where high uses the base face, hand, and gesture models and low uses INT8 quantized variants. Candidate load has two levels, corresponding to one-bystander and two-bystander stimuli. The dependent variables are per-frame processing time, module-level processing time, FPS, intent-to-enforcement processing time, and intent-to-enforcement correctness.

To run the experiment, open the Cardea-inspired PET Unity project for Magic Leap 2 or Meta Quest 3. Configure the project to use the selected model stack and run EvaluatAR in Replay mode using the corresponding recorded input data. Replay a one-bystander video and the two-bystander video under both model stack configurations.

After collecting the replay logs, open `Analysis.ipynb`, set the input paths to the newly generated logs, and run the notebook cells for Experiment 2. The analysis should generate the module-level timing, intent-to-enforcement correctness, FPS, and intent-to-enforcement processing-time summaries corresponding to Figure 6, Figure 7, Figure 9, and Figure 10 in the paper. The expected result is that ML2 achieves better runtime performance than MQ3, the low-precision stack does not reduce end-to-end processing time, and intent-to-enforcement correctness becomes less stable in the more demanding two-bystander and low-precision conditions. In the paper, this experiment used two videos, two model stacks, two headsets, and three repetitions per condition, resulting in 24 total trials.

#### Experiment 3: Face association failures and proposed modifications
- Time: approximately 75 human-minutes per PET per headset

This experiment reproduces Main Result 3. In this experiment, we instantiate EvaluatAR with BystandAR and vary two independent variables: edge-case visual stimulus and face association logic. The edge-case stimulus has three levels: overlapping faces, slow crossing faces, and fast crossing faces. The association logic has five levels: baseline, Naive Predicted Position, Closest Depth, Kalman Predicted Position, and Hybrid. The dependent variable is identity/state continuity, measured as per-trial pass/fail and summarized across repetitions.

To run the experiment, open the BystandAR-based Unity project on Magic Leap 2. For each edge-case stimulus, replay the same recorded inputs while changing only the association logic inside the PET. Each association logic should be run ten times per edge-case stimulus.

After collecting the replay logs, open `Analysis.ipynb`, set the input paths to the newly generated logs, and run the notebook cells for Experiment 3. The analysis should generate the pass/fail summary corresponding to Table 2 in the paper. EvaluatAR’s synchronized visual overlays can also be used to inspect the failure modes shown in Figure 8, including swapped identities, lost-and-recreated identities, and drift/misassignment after occlusion. The expected result is that the baseline association logic fails frequently under overlapping and crossing faces, while Kalman Predicted Position achieves 10/10 passes across all three edge-case scenarios. In the paper, this experiment used three edge-case videos, five association logics, and ten repetitions per condition, resulting in 150 total trials.

## Limitations (Required for Functional and Reproduced badges)
The submitted artifact provides the EvaluatAR framework implementation, the reference PET instantiations, and the analysis notebooks used to process framework-generated logs. However, the artifact does not redistribute the raw visual stimuli or the framework-generated logs used in the paper. These materials may contain identifiable people, private environments, or commercially licensed footage. As discussed in the paper, EvaluatAR’s responsible use depends on consented, licensed, or synthetic stimuli, and we therefore provide the framework code, analysis scripts, and stimulus-source information rather than redistributing raw scenario videos.

Moreover, as the original stimuli and logs are not included, the exact numerical values in the paper’s plots and tables are not directly reproducible from the GitHub repository alone. This applies to Figures 4 and 5 for the BystandAR cross-headset FPS experiments, Figures 6, 7, 9, and 10 for the Cardea-inspired explicit PET experiments, and Table 2 for the BystandAR association-logic comparison. Users can reproduce the workflow and generate comparable outputs by collecting new logs with licensed, consented, or synthetic stimuli that follow the same experimental structure.

## Notes on Reusability (Encouraged for all badges)
EvaluatAR is intended to be reused beyond the specific experiments reported in the paper. The core framework is implemented as a modular Unity/C# script that can be integrated with other Unity-based bystander PETs through input/output hooks. A researcher developing a new PET can expose the sensor inputs consumed by their PET, pass the PET’s outputs to EvaluatAR’s logging interface, and use the same collect-replay workflow to evaluate the PET under controlled conditions.

The artifact can also be adapted to new visual stimuli and evaluation scenarios. The experiments in the paper use stock, author-recorded, and synthetic videos, but the framework does not depend on those specific stimuli. Future users can replace them with their own licensed, consented, or synthetic recordings to evaluate different bystander privacy scenarios, environments, motion patterns, numbers of people, or privacy-relevant edge cases.

EvaluatAR can also support additional PET output measures. In our case studies, we log metrics such as FPS, detected face regions, bystander/subject labels, gesture detections, obfuscation states, and module-level processing times. Researchers can extend the logged fields to include other PET-specific outputs, such as segmentation masks, confidence scores, policy decisions, privacy transformations, or additional timing measurements. The provided analysis notebooks can then be used as templates for processing these new logs and generating corresponding plots or summary tables.

Importantly, several components of EvaluatAR can also be reused independently in other XR evaluation workflows. For example, the marker-based pose recreation mechanism can help preserve a consistent user or headset viewpoint across repeated trials. This functionality is useful beyond bystander PET evaluation because many XR studies are sensitive to viewpoint differences: small changes in headset pose can change visible scene content, gaze rays, object positions, and sensor readings. Researchers evaluating gaze-based interfaces, embodied interaction techniques, spatial perception tasks, AR visualizations, or context-aware XR systems could reuse this component to reduce trial-to-trial variation without needing to reproduce the entire EvaluatAR pipeline.