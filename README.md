# obj-editor-unity
a text based 3D model editor following basic syntax of the .obj format
The .obj file format is essentially a text file. 
simple-obj-editor provides an interface to edit your .obj file and see the changes live.

# build link: https://hmnt.itch.io/obj-editor

# a brief how-to:
1. lines starting with v and followed by three numbers seperated by whitespace are vertices
2. lines starting with vn and followed by three numbers seperated by whitespace are vertex normals which decide the how light calculation works.
3. lines starting with f and followed by three values in the format of
f    v/vt/vn     v/vt/vn      v/vt/vn
are faces with v being vertex, vn being vertex normals and vt being vertex textures
(vertex textures, beziers and other .obj features are not used in this app for simplicity purposes).
we define three vertices in one face line as this doesn't directly support triangulation

for more details: https://cs.wellesley.edu/~cs307/readings/obj-ojects.html

# Open Source Licences

https://github.com/ogxd/normals-effect-unity

https://github.com/xttx/Unity-Runtime-ScriptEditor/tree/master/Script-Editor
