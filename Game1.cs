// Todo:
// Implement parallel array:
// One array of colours so that the texture2d display_colour will work
// One array of lists of lines present at every point

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Riptide.Utils;

namespace laser;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private const int screenWidth = 1000;
    private const int screenHeight = 600;
    private bool lmb_last_frame = false;
    private bool rmb_last_frame = false;
    private Vector mouse_pos;

    private Color[] display_color = new Color[screenWidth*screenHeight]; // the pixels on the screen and their colour (doesn't include laser pixels)
    private LinkedList<int>[] coord_line = new LinkedList<int>[screenWidth*screenHeight]; // lines that exist at any point
    private List<LinkedList<Vector>> line_data = new List<LinkedList<Vector>>(); // the points on each line
    private Texture2D display_texture;
    private int current_line = 0;
    private bool can_move_to_next_line = false;

    private Color[] laser_data = new Color[screenWidth*screenHeight];
    private Texture2D laser_texture;
    private Vector last_draw;
    private const int erase_radius = 10;
    private const float pi = (float)Math.PI;

    // CONSTRUCTOR ---------------------------------------------------------------------------------------------------------------------------------------------------
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    // INITIALISATION ----------------------------------------------------------------------------------------------------------------------------------------------
    protected override void Initialize()
    {
        base.Initialize();
        CreateBlackDisplay();
        line_data.Add(new LinkedList<Vector>());
    }

    protected void CreateBlackDisplay() {
        display_texture = new Texture2D(GraphicsDevice, screenWidth, screenHeight);
        laser_texture = new Texture2D(GraphicsDevice, screenWidth, screenHeight);

        for (int i=0; i<screenWidth*screenHeight; i++) {
            display_color[i] = Color.Black;
        }

        display_texture.SetData(display_color);
        laser_texture.SetData(laser_data);
    }

    // LOAD CONTENT ---------------------------------------------------------------------------------------------------------------------------------------------
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    // UPDATE --------------------------------------------------------------------------------------------------------------------------------------------------------
    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (this.IsActive && (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))) { Exit(); }
        mouse_pos = new Vector(Mouse.GetState().Position.X, Mouse.GetState().Position.Y); // the position of the mouse click

        UpdateDrawing();
        if (this.IsActive && ((Mouse.GetState().LeftButton == ButtonState.Released && lmb_last_frame) || (Mouse.GetState().RightButton == ButtonState.Released && rmb_last_frame))) { UpdateLaser(); }

        lmb_last_frame = Mouse.GetState().LeftButton == ButtonState.Pressed; // must be the last line of the frame
        rmb_last_frame = Mouse.GetState().RightButton == ButtonState.Pressed;
    }

    protected void UpdateDrawing() {
        DrawByClicking();
        if (Mouse.GetState().LeftButton != ButtonState.Pressed) { EraseByClicking(); }
        display_texture.SetData(display_color);
    }

    protected void DrawByClicking() {
        if (Mouse.GetState().LeftButton == ButtonState.Pressed && this.IsActive) {
            can_move_to_next_line = true; // once unclicked, start new line
            if (withinScreenBounds(mouse_pos.x, mouse_pos.y)) { // if within the screen
                if ((last_draw == null) || (last_draw != null && !last_draw.Equals(mouse_pos))) {
                    DrawPixel(mouse_pos.x, mouse_pos.y, Color.White, current_line);
                    if (last_draw != null) { JoinPixelToPreviousPixel(mouse_pos); }
                    last_draw = mouse_pos;
                }
            }
        }
        else {
            if (can_move_to_next_line) {
                last_draw = null;
                line_data.Add(new LinkedList<Vector>());
                current_line++;
                can_move_to_next_line = false;
            }
        }
    }

    protected void DrawPixel(int x, int y, Color color, int line) {
        display_color[y*screenWidth+x] = color;
        if (line != -1) {
            if (coord_line[y*screenWidth+x] == null) { coord_line[y*screenWidth + x] = new LinkedList<int>(); }
            coord_line[y*screenWidth+x].AddLast(line);
            line_data[line].AddLast(new Vector(x, y));
        }
    }

    protected void JoinPixelToPreviousPixel(Vector pos) {
        Vector line = last_draw - pos;
        Vector2 line_unit = line.Normalise();
        int multiplier = 1;
        while ((line_unit*multiplier).Length() < line.Length()) {
            int new_x = (int)(pos.x + line_unit.X*multiplier);
            int new_y = (int)(pos.y + line_unit.Y*multiplier);
            if (!((new_x == last_draw.x && new_y == last_draw.y) || (new_x == pos.x && new_y == pos.y))) { DrawPixel(new_x, new_y, Color.White, current_line); }
            multiplier++;
        }
    }

    protected void EraseByClicking() {
        // Delete line if it is entirely erased
        // IE: the point that was removed was the last point on the line, therefore the empty linkedlist representing the line serves no purpose and can be removed
            // Will this have any issues with indexing? IE: removing this point means the previous point will take its place be offset from its proper position
                // Probably so. Lines are found by linearly iterating over line_data to the correct line id
        // It also has to be removed from coord_line too

        // TODO:
        // Remove data about erased lines
        // Split the remaining pixels in the line into two different lines
        // Set the current line collection to null
        // Run checks for those types elsewhere maybe?

        // Scrap all that. I think I only need to do two things:
        // Make the erased pixels black
        // Clear the data about what lines are passing through each erased pixel in coord_line
            // These lines cannot be interacted with again, but can be used to calculate gradients (ie: hitting a white pixel next to an erased pixel will use the erased pixel in the calculation of the gradient, despite the fact that pixel cannot be hit itself anymore)

        if (Mouse.GetState().RightButton == ButtonState.Pressed && this.IsActive && withinScreenBounds(mouse_pos)) {
            // Repeat process for every pixel within the erasion square
            for (int x=Mouse.GetState().Position.X-erase_radius; x<=Mouse.GetState().Position.X+erase_radius; x++) {
                for (int y=Mouse.GetState().Position.Y-erase_radius; y<=Mouse.GetState().Position.Y+erase_radius; y++) {
                    if (coord_line[screenWidth*y+x] != null) { coord_line[screenWidth*y+x].Clear(); }
                    display_color[screenWidth*y+x] = Color.Black;
                }
            }
        }
    }

    public bool is_first_in_collection<T>(LinkedList<T> collection, T value) {
        return collection.First.Value.Equals(value);
    }

    public bool is_last_in_collection<T>(LinkedList<T> collection, T value) {
        LinkedListNode<T> node = collection.First;
        while (node.Next != null) { node = node.Next; }
        return node.Value.Equals(value);
    }

    protected void SetLaserPixelColour(int x, int y) {
        laser_data[y*screenWidth+x] = Color.Red;
    }

    protected void SetLaserPixelColour(int x, int y, Color color) {
        laser_data[y*screenWidth+x] = color;
    }

    protected void UpdateLaser() {
        // to-do: only update parts of laser that are affected by new drawing to save computational power

        float angle = 0; // angle in radians from the horizontal, where anticlockwise is positive
        int x_init = 0;
        int y_init = 300;
        int x = 300;
        int y = 0;
        int total_pixels_to_get_away_from_mirror = 0;
        int regression_pixel_count = 7;
        int regression_pixel_count_small = 3;
        int bounces = 0;
        int bounce_limit = 100;
        float acceptable_approximation_difference = pi/12;

        laser_data = new Color[screenHeight*screenWidth]; // Reset it so that the old laser lines don't show after they've been re-written. Remove because of optimisation later
        Console.WriteLine("--------------------------------------------------");
        while (withinScreenBounds(x, y)) {
            Console.WriteLine();
            // move laser around until goes offscreen or hits a mirror
            // if hits a mirror, then reset x_init and y_init and calculate x and y from there
            (x, y, Vector mirror_hit, char offscreen) = drawStraightLaserUntilInterruption(total_pixels_to_get_away_from_mirror, x_init, y_init, angle);
            if (offscreen == 'o') { return; }
            x_init = x;
            y_init = y;
            SetLaserPixelColour(x, y, Color.PaleVioletRed);
            
            // Find line that was hit
            LinkedList<int> line_ids = coord_line[mirror_hit.y*screenWidth + mirror_hit.x];
            int line_id = line_ids.First.Value;
            LinkedList<Vector> line_hit = line_data[line_id];

            // Calculate the angle of reflection
            float old_angle = angle;
            if (offscreen == 'b') { angle = angle + pi; }
            else {
                float line_gradient = findLineGradient(mirror_hit, regression_pixel_count, line_hit);
                float surface_angle = (float)Math.Atan(-line_gradient);

                // Should the difference between the large and small approximations be too great, the small approximation is used instead
                float line_gradient_small = findLineGradient(mirror_hit, regression_pixel_count_small, line_hit);
                float surface_angle_small = (float)Math.Atan(-line_gradient_small);
                if (acceptable_approximation_difference < Math.Abs(surface_angle-surface_angle_small)) {
                    surface_angle = surface_angle_small;
                    Console.WriteLine("Used regression_small");

                }

                Console.WriteLine($"line_gradient: {line_gradient}");

                surface_angle = (float.IsNaN(surface_angle)) ? surface_angle = pi/2 : ValidateAngle(surface_angle);
                float ts = 0;
                float tb = 0;
                if (surface_angle > Math.PI) {
                    tb = surface_angle;
                    ts = surface_angle-pi;
                }
                else {
                    ts = surface_angle;
                    tb = surface_angle+pi;
                }

                char side = (ts < angle && angle < tb) ? 'b' : 'a'; // Which side the laser is hitting from
                float s = ValidateAngle((side == 'a') ? tb+pi/2 : tb-pi/2); // Angle of the normal. The direction of the normal is defined as towards the surface from the side of the laser
                Console.WriteLine($"Side: {side}");
                Console.WriteLine($"ts: {ts*180/pi}, tb: {tb*180/pi}");
                float Δangle = ValidateAngle(Math.Abs(angle-s));
                if (side == 'a') {
                    Δangle = ValidateAngle(Math.Abs(((angle<ts) ? angle + 2*pi : angle)-((s<ts) ? s + 2*pi : s)));
                }

                Console.WriteLine($"Surface angle: {surface_angle*180/pi}\ns: {s*180/pi}\nΔangle: {Δangle*180/pi}\nincident laser: {angle*180/pi}");

                if (side == 'a') {
                    angle = (((angle<ts) ? angle + 2*pi : angle) < ((s<ts) ? s + 2*pi : s)) ? angle + pi + 2*Δangle : angle + pi - 2*Δangle;
                }
                else {
                    angle = (angle < s) ? angle + pi + 2*Δangle : angle + pi - 2*Δangle;
                }
                
                // Draw the normal
                /*
                int norm_len = 0;
                for (int i=0; i<norm_len; i++) {
                    SetLaserPixelColour(x_init+(int)(i*Math.Cos(surface_angle)), y_init-(int)(i*Math.Sin(surface_angle)), Color.LightGreen);
                }
                for (int i=0; i<norm_len; i++) {
                    SetLaserPixelColour(x_init-(int)(i*Math.Cos(surface_angle)), y_init+(int)(i*Math.Sin(surface_angle)), Color.LightGreen);
                }
                for (int i=0; i<norm_len; i++) {
                    SetLaserPixelColour(x_init+(int)(i*Math.Cos(s)), y_init-(int)(i*Math.Sin(s)), Color.LightBlue);
                }
                for (int i=0; i<norm_len; i++) {
                    SetLaserPixelColour(x_init-(int)(i*Math.Cos(s)), y_init+(int)(i*Math.Sin(s)), Color.LightBlue);
                }
                */
            }

            // Update laser angle
            angle = ValidateAngle(angle);

            // If reached bounce limit, stop reflecting
            bounces++;
            if (bounce_limit <= bounces) { break; }

            // Calculate next XY and see if laser will reflect properly. If not, write some logic so the laser will reflect differently
            (int x_next, int y_next) = calculateNextXY(x_init, y_init, angle);
            if (!withinScreenBounds(x_next, y_next)) { break; }
            SetLaserPixelColour(x_next, y_next, Color.Orange);

            Console.WriteLine($"resultant laser: {angle*180/pi}");
            bool failed_reflection = false;
            
            if (!IsDisplayPixelBlack(x_next, y_next)) { failed_reflection = true; }

            if (line_hit.Count > 1) {
                LinkedListNode<Vector> point_on_line = line_hit.First;
                LinkedListNode<Vector> prev_point_on_line = point_on_line;
                while (!point_on_line.Value.Equals(mirror_hit)) {
                    prev_point_on_line = point_on_line;
                    point_on_line = point_on_line.Next;
                }

                if (point_on_line.Value.Equals(prev_point_on_line.Value) && point_on_line.Next != null) {
                    if (makesSquarePattern(new Vector(x_init, y_init), new Vector(x_next, y_next), mirror_hit, point_on_line.Next.Value)) { failed_reflection = true; }
                }
                else if (point_on_line.Next == null) {
                    if (makesSquarePattern(new Vector(x_init, y_init), new Vector(x_next, y_next), mirror_hit, prev_point_on_line.Value)) { failed_reflection = true; }
                }
                else {
                    if (makesSquarePattern(new Vector(x_init, y_init), new Vector(x_next, y_next), mirror_hit, prev_point_on_line.Value) || makesSquarePattern(new Vector(x_init, y_init), new Vector(x_next, y_next), mirror_hit, point_on_line.Next.Value)) { failed_reflection = true; }
                }
            }

            // Reflect better if reflection logic failed
            if (failed_reflection) {
                Console.WriteLine("doing goofy things");
                angle = old_angle;
                // Potential bug, because even though there's a pixel above, that doesn't guarantee the laser's moving upwards and so will hit that pixel
                // And if it's not moving up initially, perhaps another one of the four reflections below will cause it to
                if (IsDisplayPixelBlack(x_init+1, y_init)) { angle = ValidateAngle((angle <= pi) ? angle + 2*(pi-angle) + pi : angle - 2*(angle-pi) + pi); }
                if (IsDisplayPixelBlack(x_init, y_init+1)) { angle = ValidateAngle((angle <= pi/2) ? angle + 2*(pi/2-angle) + pi : angle - 2*(angle-pi/2) + pi); }
                if (IsDisplayPixelBlack(x_init-1, y_init)) { angle = ValidateAngle(pi-angle); }
                if (IsDisplayPixelBlack(x_init, y_init-1)) { angle = ValidateAngle((angle <= 3*pi/2) ? angle + 2*(3*pi/2-angle) + pi : angle - 2*(angle-3*pi/2) + pi); }
            }

            Console.WriteLine($"resultant resultant laser: {angle*180/pi}");
        }
        laser_texture.SetData(laser_data);
    }

    protected bool withinScreenBounds(int x, int y) { return 0 <= x && x < screenWidth && 0 <= y && y < screenHeight; }
    protected bool withinScreenBounds(float x, float y) { return 0 <= x && x < screenWidth && 0 <= y && y < screenHeight; }
    protected bool withinScreenBounds(Vector pos) { return 0 <= pos.x && pos.x < screenWidth && 0 <= pos.y && pos.y < screenHeight; }

    protected bool sharedValue<T>(LinkedList<T> list1, LinkedList<T> list2) {
        LinkedListNode<T> node = list1.First;
        bool shared_value = false;
        while (node.Next != null && !shared_value) {
            if (list2.Contains(node.Value)) { shared_value = true; }
            node = node.Next;
        }
        return shared_value;
    }

    protected (int, int, Vector, char) drawStraightLaserUntilInterruption(int total_pixels_to_get_away_from_mirror, int x_init, int y_init, float angle) {
        int n=1;
        int x = x_init;
        int y = y_init;
        Vector mirror_hit = new Vector();

        while (true) {
            (int x_next, int y_next) = calculateNextXY(x_init, y_init, angle, n);

            if (!withinScreenBounds(x_next, y_next)) {
                laser_texture.SetData(laser_data);
                return (x, y, mirror_hit, 'o');
            }

            if (!(n<total_pixels_to_get_away_from_mirror)) {
                if (!IsDisplayPixelBlack(x_next, y_next)) {
                    mirror_hit = new Vector(x_next, y_next);
                    return (x, y, mirror_hit, 'r');
                }
                else if (Math.Sin(angle) != 0 && Math.Cos(angle) != 0) { // if the laser is going in a straight horizontal or vertical line, the following check doesn't need to be done
                    int x_dirmod = (Math.Cos(angle)>0) ? 1 : -1; // This check determines if the two adjacent pixels in the direction of the laser are on the same line
                    int y_dirmod = (-Math.Sin(angle)>0) ? 1 : -1; // Since pixels are quanta, the laser may pass through the line as it doesn't directly hit a pixel
                    if (withinScreenBounds(x, y+y_dirmod) && withinScreenBounds(x+x_dirmod, y)) {
                        LinkedList<int> vertical_adjacent_pixel = coord_line[(y+y_dirmod)*screenWidth+x]; // This check makes sure that the laser still registers the line
                        LinkedList<int> horizontal_adjacent_pixel = coord_line[y*screenHeight+(x+x_dirmod)]; // The first four lines get the line data for the adjacent pixels
                        if (!IsDisplayPixelBlack(x, y+y_dirmod) && !IsDisplayPixelBlack(x+x_dirmod, y)) {
                            mirror_hit = new Vector(x, y+y_dirmod); // This chooses only one of the adjacent pixels
                            return (x, y, mirror_hit, 'b');
                        }
                    }
                }
            }
            
            x = x_next;
            y = y_next;
            SetLaserPixelColour(x, y);
            n++;
        }
    }

    protected float findLineGradient(Vector point_to_be_found, int regression_pixel_count, LinkedList<Vector> line) {
        float line_gradient = 0;

        // Find the regression if the line length is less than a specified hyperparameter
        if (line.Count <= regression_pixel_count) { line_gradient = linearRegression(line); }
        else {
            // Find the regression if the line length is more than the hyperparamer. This is done in the following manner:
            Vector[] points = new Vector[regression_pixel_count];
            LinkedListNode<Vector> point_on_line = line.First;

            while (point_on_line.Next != null && ((points[(int)(regression_pixel_count/2)] != null && !points[(int)(regression_pixel_count/2)].Equals(point_to_be_found)) || HasNull(points))) {
                for (int i=regression_pixel_count-2; i>=0; i--) { points[i+1] = points[i]; }
                points[0] = point_on_line.Value;
                point_on_line = point_on_line.Next;
            }

            LinkedList<Vector> line_to_linearly_regress = new LinkedList<Vector>();
            for (int j=0; j<regression_pixel_count; j++) {
                line_to_linearly_regress.AddLast(points[j]);
            }
            line_gradient = linearRegression(line_to_linearly_regress);
        }

        return line_gradient;
    }

    protected bool HasNull<T>(T[] arr) {
        int i=0;
        while (i<arr.Length) {
            if (arr[i] == null) { return true; }
            i++;
        }
        return false;
    }

    protected float linearRegression(LinkedList<Vector> points) {
        int n = points.Count;
        float sum_x = 0;
        float sum_y = 0;
        float sum_x_y = 0;
        float sum_x_squared = 0;

        LinkedListNode<Vector> point = points.First;
        while (point != null) {
            sum_x += point.Value.x;
            sum_y += point.Value.y;
            sum_x_y += point.Value.x * point.Value.y;
            sum_x_squared += point.Value.x * point.Value.x;
            point = point.Next;
        }
        return (n*sum_x_y - sum_x*sum_y) / (n*sum_x_squared - sum_x*sum_x);
    }

    protected float ValidateAngle(float angle) {
        while (!(0 <= angle && angle < 2*Math.PI)) {
            if (angle < 0) { angle += 2*(float)Math.PI; }
            else if (angle >= 2*(float)Math.PI) { angle -= 2*(float)Math.PI; }
        }
        return angle;
    }

    protected bool IsDisplayPixelBlack(int x, int y) { return display_color[y*screenWidth + x] == Color.Black; }

    protected (int, int) calculateNextXY(int x_init, int y_init, float angle, int n=1) {
        int y_next = (int)(y_init - n*Math.Sin(angle));
        int x_next = (int)(x_init + n*Math.Cos(angle));
        return (x_next, y_next);
    }

    protected bool makesSquarePattern(Vector aa, Vector ab, Vector ba, Vector bb) {
        Vector current_pixel = aa;
        if (aa.IsDiagonallyDownwards(ab)) {
            if ((aa.IsAdjacentlyBeneath(ba) && aa.IsAdjacentlyToTheRight(bb)) && (aa.IsAdjacentlyBeneath(bb) && aa.IsAdjacentlyToTheRight(ba))) { return true; }
        }
        else if (ab.IsDiagonallyDownwards(aa)) {
            if ((ab.IsAdjacentlyBeneath(ba) && ab.IsAdjacentlyToTheRight(bb)) && (ab.IsAdjacentlyBeneath(bb) && ab.IsAdjacentlyToTheRight(ba))) { return true; }
        }
        else if (ba.IsDiagonallyDownwards(bb)) {
            if ((ba.IsAdjacentlyBeneath(aa) && ba.IsAdjacentlyToTheRight(ab)) && (ba.IsAdjacentlyBeneath(aa) && ba.IsAdjacentlyToTheRight(ab))) { return true; }
        }
        else if (bb.IsDiagonallyDownwards(ba)) {
            if ((bb.IsAdjacentlyBeneath(ab) && bb.IsAdjacentlyToTheRight(aa)) && (bb.IsAdjacentlyBeneath(ab) && bb.IsAdjacentlyToTheRight(aa))) { return true; }
        }
        return false;
    }

    // DRAW ----------------------------------------------------------------------------------------------------------------------------------------------
    protected override void Draw(GameTime gameTime)
    {
        // GraphicsDevice.Clear(Color.CornflowerBlue);
        // base.Draw(gameTime);

        _spriteBatch.Begin();

        _spriteBatch.Draw(display_texture, Vector2.Zero, Color.White);
        _spriteBatch.Draw(laser_texture, Vector2.Zero, Color.White);

        _spriteBatch.End();
    }
}

public class Vector {
    public int x;
    public int y;
    public Vector(int x=0, int y=0) {
        this.x = x;
        this.y = y;
    }

    public bool IsAdjacentlyBeneath(Vector b) { return (this.x == b.x && this.y+1 == b.y); }
    public bool IsAdjacentlyToTheRight(Vector b) { return (this.x+1 == b.x && this.y == b.y); }
    public bool IsDiagonallyDownwards(Vector b) { return (this.x+1 == b.x && this.y+1 == b.y); }
    
    public double Length() { return Math.Sqrt(this.x*this.x + this.y*this.y); }
    public Vector2 Normalise() { return new Vector2((float)(x/Length()), (float)(y/Length())); }

    public static Vector operator -(Vector a, Vector b) {
        return new Vector(a.x-b.x, a.y-b.y);
    }

    public static Vector operator -(Vector a) { return new Vector(-a.x, -a.y); }

    public bool Equals(Vector b) { return (x==b.x && y==b.y); }

    public void String() { Console.WriteLine($"x: {x}, y: {y}"); }
}
