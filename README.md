<img src="https://github.com/user-attachments/assets/16ad69d2-d5f9-4e19-b108-2bbc44d6dec8" width="300px">

# obj-editor-unity
a text based 3D model editor following basic syntax of the .obj format

obj-editor-unity provides an interface to edit your 3d model / unity mesh / .obj file text data and see the changes live.

> [windows & linux build: https://hmnt.itch.io/obj-editor](https://hmnt.itch.io/obj-editor)

> [.obj primitives](https://gist.github.com/hemanth2004/8a22aa78e847fdfbb8ce36c7bb3031c0)


# a brief how-to:
1. lines starting with v and followed by three numbers seperated by whitespace are vertices

2. lines starting with vn and followed by three numbers seperated by whitespace are vertex normals which decide the how light calculation works.

3. lines starting with f and followed by three values in the format of

f    v/vt/vn     v/vt/vn      v/vt/vn

are faces with v being vertex, vn being vertex normals and vt being vertex textures
(vertex textures, beziers and other freeform geometry .obj features are not used in this app for simplicity purposes).
we define three vertices in one face line as this doesn't directly support triangulation

for more details: https://cs.wellesley.edu/~cs307/readings/obj-ojects.html

movement:

1. hold left mouse button and move to rotate around scene origin

2. hold middle mouse button and move to pan 

3. hold right mouse button and press W,A,S,D,Q,E for movement


# open source licences

https://github.com/ogxd/normals-effect-unity

https://github.com/xttx/Unity-Runtime-ScriptEditor/tree/master/Script-Editor

https://github.com/yasirkula/UnityRuntimePreviewGenerator

https://pastebin.com/tVdF8bNG
