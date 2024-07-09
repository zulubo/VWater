**VWater** is the system I created to render and simulate bodies of water in Vertigo 2. It is very lightweight and optimized for VR. Although it was used successfully in production, it has some idiosyncrasies and it's not quite a drag and drop solution.

<img width="883" alt="Multicolored pools of water" src="https://github.com/zulubo/VWater/assets/29665945/9eb3f423-966b-4e9e-9ce1-0522ec8f77c0">

<h3>Features</h3>

  - Physically based light absorption and scattering
  - GPU-driven ripple simulation
  - Multiple bodies of water with different water levels, materials, and physical properties
  - Underwater view rendering
  - Basic buoyancy physics for rigidbodies
  - Water masking inside boats
  - [Crest Ocean System](https://github.com/wave-harmonic/crest) compatibility

<h3>Limitations</h3>

  - Bodies of water must be axis aligned cuboids
  - Water surface mesh is a flat plane, no tesselation or displacement
  - Water flow is limited to a single direction per body of water
  - Shaders only support the built in render pipeline


<img width="400" alt="View halfway underwater" src="https://github.com/zulubo/VWater/assets/29665945/e33e5882-2f7d-422d-be94-9d7ea6a1ccc7"> 
<img width="400" alt="A boat" src="https://github.com/zulubo/VWater/assets/29665945/c8463b27-7895-43e9-ac43-be8a217f6b34">

<h2>User Guide</h2>

Before you start, make sure there is a VWaterManager and VWaterDynamicsManager instance in your scene. There is a prefab with both of these scripts you can use.

<h4>Creating a body of water</h4>

You can use any mesh for a body of water, but the most basic place to start is a plane. Assign a VWater material to the plane, and add a second material slot for the "VWater Bottom" material (ignore the warning) if you want to render underwater. 
The water needs a VWater component, a VWaterRenderer component, and a trigger box collider (or multiple!) to define the shape of the water.

You can scale the water non uniformly, the material uses world space UVs.

There is a basic water plane included in the Prefabs folder. I rarely use anything other than the basic plane, but you can use other meshes for waterfalls and such. 
Note that the water physics and rendering will still treat the water as a plane, however.

<h4>Underwater Rendering</h4>

Underwater rendering is handled automatically whenever a camera enters a body of water. You shouldn't need to worry about it. 
You can toggle it off for each body of water if you want to, and you should if you know the player will never enter a certain body of water.

<h4>Adding Water Physics</h4>

To add buoyancy physics to a rigidbody, add the WaterBody component. Set the "Main Collider" field to the rigidbody's largest collider. If it has a lot of small colliders, consider adding a large encompassing collider, setting it as a trigger, and using that.

If you want the rigidbody to leave ripples in the water, add a VWaterDynamicFlowSource component, and set the WaterBody "Wake" field to that component.

To add ripples from other sources, you can use independent VWaterDynamicFlowSource components, or call VWaterDynamicsManager.instance.AddFlow() yourself.

<h4>Masking</h4>

Masking is used to cut holes in the surface of the water, which is most useful for boats. See the included boat prefab for an example.

Masks need a MeshRenderer with the VWaterMask material, a trigger, and a VWaterMask component

<h4>Character Interaction With Water</h4>

How your character interacts with water depends on the game. I've commented out the code in WaterBody that would normally handle character submersion. 
I implemented swimming physics in the character controller itself, so I've left that out of this repository completely. 
In the "Extras" folder I've included the script, WaterHead, that I use to play sounds and muffle audio when your head goes underwater, and also simulate drowning. You'll have to do some extra work to get it working though.
