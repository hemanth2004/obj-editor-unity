<img src="https://github.com/user-attachments/assets/16ad69d2-d5f9-4e19-b108-2bbc44d6dec8" width="225px">

<img src="https://github.com/user-attachments/assets/b36419d7-a577-4e7c-92d4-227b52a39e18" width="260px">

<img src="https://github.com/user-attachments/assets/fe086dda-b8bb-465f-bf76-df0726c0cb73" width="260px">



# obj-editor-unity
a text based 3D model editor following basic syntax of the .obj format

obj-editor provides an interface to edit your mesh data and see the changes live instead of having to open the .obj in a tool like blender with every change you make to the raw text.

> [windows & linux build (itch.io)](https://hmnt.itch.io/obj-editor)

## a brief how-to
1. lines starting with v and followed by three numbers seperated by whitespace are vertices
2. lines starting with vn and followed by three numbers seperated by whitespace are vertex normals which decide the how light calculation works.
3. lines starting with f and followed by three values in the format of `f v/vt/vn v/vt/vn v/vt/vn` are faces with v being vertex, vn being vertex normals and vt being vertex textures
(vertex textures, beziers and other freeform geometry .obj features are not used in this app for simplicity purposes).
we define three vertices in one face line as this doesn't directly support triangulation

> for samples of shapes: [.obj primitives](https://gist.github.com/hemanth2004/8a22aa78e847fdfbb8ce36c7bb3031c0)

> for more details: https://cs.wellesley.edu/~cs307/readings/obj-ojects.html

## controls
1. hold left mouse button and move to rotate around scene origin
2. hold middle mouse button and move to pan 
3. hold right mouse button and -
   1. move mouse to look around in FPV
   2. press W,A,S,D,Q,E for movement


## open source licences

https://github.com/ogxd/normals-effect-unity

https://github.com/xttx/Unity-Runtime-ScriptEditor/tree/master/Script-Editor

https://github.com/yasirkula/UnityRuntimePreviewGenerator

https://pastebin.com/tVdF8bNG
