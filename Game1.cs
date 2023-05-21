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

    private Color[] display_color = new Color[screenWidth*screenHeight]; // the pixels on the screen and their colour (doesn't include laser pixels)
    private LinkedList<int>[] coord_line = new LinkedList<int>[screenWidth*screenHeight]; // lines that exist at any point
    private List<LinkedList<Vector2>> line_data = new List<LinkedList<Vector2>>(); // the points on each line
    private Texture2D display_texture;
    private int current_line = 0;
    private bool canMoveToNextLine = false;

    private Color[] laser_data = new Color[screenWidth*screenHeight];
    private Texture2D laser_texture;
    private Vector2 lastDraw;
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
        line_data.Add(new LinkedList<Vector2>());
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
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape)) { Exit(); }

        UpdateDrawing();
        if (Mouse.GetState().LeftButton == ButtonState.Released && lmb_last_frame && this.IsActive) { UpdateLaser(); } // Update laser
        base.Update(gameTime);

        lmb_last_frame = Mouse.GetState().LeftButton == ButtonState.Pressed; // must be the last line of the frame
    }

    protected void UpdateDrawing() {
        DrawByClicking();
        EraseByClicking();
        display_texture.SetData(display_color);
    }

    protected void DrawByClicking() {
        if (Mouse.GetState().LeftButton == ButtonState.Pressed && this.IsActive) {
            canMoveToNextLine = true; // once unclicked, start new line
            Vector2 pos = new Vector2(Mouse.GetState().Position.X, Mouse.GetState().Position.Y); // the position of the mouse click
            if (withinScreenBounds(pos.X, pos.Y)) { // if within the screen
                DrawPixel((int)pos.X, (int)pos.Y, Color.White, current_line);
                if (lastDraw != Vector2.Zero) { JoinPixelToPreviousPixel(pos); }
                lastDraw = pos;
            }
        }
        else {
            if (canMoveToNextLine) {
                lastDraw = Vector2.Zero;
                line_data.Add(new LinkedList<Vector2>());
                current_line++;
                canMoveToNextLine = false;
            }
        }
    }

    protected void DrawPixel(int x, int y, Color color, int line) {
        display_color[y*screenWidth+x] = Color.White;
        if (coord_line[y*screenWidth+x] == null) { coord_line[y*screenWidth + x] = new LinkedList<int>(); }
        coord_line[y*screenWidth+x].AddLast(current_line);
        line_data[current_line].AddLast(new Vector2(x, y));
    }

    protected void JoinPixelToPreviousPixel(Vector2 pos) {
        Vector2 line = lastDraw - pos;
        Vector2 line_unit = Vector2.Normalize(line);
        int multiplier = 1;
        while ((line_unit*multiplier).Length() < line.Length()) {
            DrawPixel((int)(pos.X + line_unit.X*multiplier), (int)(pos.Y + line_unit.Y*multiplier), Color.White, current_line);
            multiplier++;
        }
    }

    protected void EraseByClicking() {
        // Delete line if it is entirely erased
        // IE: the point that was removed was the last point on the line, therefore the empty linkedlist representing the line serves no purpose and can be removed
        // It also has to be removed from coord_line too

        if (Mouse.GetState().RightButton == ButtonState.Pressed) {
            for (int x=Mouse.GetState().Position.X-erase_radius; x<=Mouse.GetState().Position.X+erase_radius; x++) {
                for (int y=Mouse.GetState().Position.Y-erase_radius; y<=Mouse.GetState().Position.Y+erase_radius; y++) {
                    DrawPixel(x, y, Color.Black, -1);
                }
            }
        }
    }

    protected void SetLaserPixelColour(int x, int y) {
        laser_data[y*screenWidth+x] = Color.Red;
    }

    protected void SetLaserPixelColour(int x, int y, Color color) {
        laser_data[y*screenWidth+x] = color;
    }

    protected void UpdateLaser() {
        // define the following:
        // some sort of gradient that indicates the direction of the line

        // add flag to only update laser when necessary
        // also only update parts of laser that are affected by new drawing to save computational power

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

        laser_data = new Color[screenHeight*screenWidth]; // Reset it so that the old laser lines don't show after they've been re-written. Remove because of optimisation later
        while (withinScreenBounds(x, y)) {
            // move laser around until goes offscreen or hits a mirror
            // if hits a mirror, then reset x_init and y_init and calculate x and y from there
            (x, y, Vector2 mirror_hit, char offscreen) = drawStraightLaserUntilInterruption(total_pixels_to_get_away_from_mirror, x_init, y_init, angle);
            if (offscreen == 'o') { return; }
            x_init = x;
            y_init = y;
            SetLaserPixelColour(x, y, Color.PaleVioletRed);

            if (offscreen == 'b') { angle = angle + pi; }
            else {
                float line_gradient = findLineGradient(mirror_hit, regression_pixel_count);
                float line_gradient_small = findLineGradient(mirror_hit, regression_pixel_count_small);

                float surface_angle = (float)Math.Atan(-line_gradient);
                float surface_angle_small = (float)Math.Atan(-line_gradient_small);

                if ((surface_angle > angle) && !(surface_angle_small > angle)) { surface_angle = surface_angle_small; }
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

                int norm_len = 0;
                for (int i=0; i<norm_len; i++) {
                    SetLaserPixelColour(x_init+(int)(i*Math.Cos(surface_angle)), y_init-(int)(i*Math.Sin(surface_angle)), Color.Green);
                }
                for (int i=0; i<norm_len; i++) {
                    SetLaserPixelColour(x_init-(int)(i*Math.Cos(surface_angle)), y_init+(int)(i*Math.Sin(surface_angle)), Color.Green);
                }


                char side = (ts < angle && angle < tb) ? 'b' : 'a'; // Which side the laser is hitting from
                float s = ValidateAngle((side == 'a') ? tb+pi/2 : tb-pi/2); // Angle of the normal. The direction of the normal is defined as towards the surface from the side of the laser
                float Δangle = ValidateAngle(Math.Abs(angle-s));
                if (Δangle > pi) { Δangle = Δangle-pi; }

                /*
                if (side == 'a') {
                    Δangle = ValidateAngle((s<angle || angle<s-3*pi/2) ? angle-s : s-angle);
                }
                else {
                    Δangle = ValidateAngle((angle>s && angle<s+pi/2) ? angle-s : s-angle);
                }
                */
                
                Console.WriteLine($"Surface angle: {surface_angle*180/pi}, s: {s*180/pi}, Δangle: {Δangle*180/pi}, incident laser: {angle*180/pi}");

                /*
                if (side == 'a') {
                    angle = (angle < s) ? angle + pi + 2*Δangle : angle + pi - 2*Δangle;
                }
                else {
                    angle = (angle < s) ? angle + pi - 2*Δangle : angle + pi + 2*Δangle;
                }
                */

                angle = (angle < s) ? angle + pi + 2*Δangle : angle + pi - 2*Δangle;
            }
            angle = ValidateAngle(angle);

            bounces++;
            if (bounce_limit <= bounces) { break; }
            // if multiple lines at one point, calculate reflection for each of them,
            // stop reflecting if reverse angle is current angle ±π radians
        }

        laser_texture.SetData(laser_data);
    }

    protected bool withinScreenBounds(int x, int y) { return 0 <= x && x < screenWidth && 0 <= y && y < screenHeight; }
    protected bool withinScreenBounds(float x, float y) { return 0 <= x && x < screenWidth && 0 <= y && y < screenHeight; }

    protected bool sharedValue<T>(LinkedList<T> list1, LinkedList<T> list2) {
        LinkedListNode<T> node = list1.First;
        bool shared_value = false;
        while (node.Next != null && !shared_value) {
            if (list2.Contains(node.Value)) { shared_value = true; }
            node = node.Next;
        }
        return shared_value;
    }

    protected (int, int, Vector2, char) drawStraightLaserUntilInterruption(int total_pixels_to_get_away_from_mirror, int x_init, int y_init, float angle) {
        int n=1;
        int x = x_init;
        int y = y_init;
        Vector2 mirror_hit = Vector2.Zero;

        while (true) {
            int y_next = (int)(y_init - n*Math.Sin(angle));
            int x_next = (int)(x_init + n*Math.Cos(angle));

            if (!withinScreenBounds(x_next, y_next)) {
                laser_texture.SetData(laser_data);
                return (x, y, mirror_hit, 'o');
            }

            if (!(n<total_pixels_to_get_away_from_mirror)) {
                if (!IsDisplayPixelBlack(x_next, y_next)) {
                    mirror_hit = new Vector2(x_next, y_next);
                    return (x, y, mirror_hit, 'r');
                }
                else if (Math.Sin(angle) != 0 && Math.Cos(angle) != 0) { // if the laser is going in a straight horizontal or vertical line, the following check doesn't need to be done
                    int x_dirmod = (Math.Cos(angle)>0) ? 1 : -1; // This check determines if the two adjacent pixels in the direction of the laser are on the same line
                    int y_dirmod = (-Math.Sin(angle)>0) ? 1 : -1; // Since pixels are quanta, the laser may pass through the line as it doesn't directly hit a pixel
                    if (withinScreenBounds(x, y+y_dirmod) && withinScreenBounds(x+x_dirmod, y)) {
                        LinkedList<int> vertical_adjacent_pixel = coord_line[(y+y_dirmod)*screenWidth+x]; // This check makes sure that the laser still registers the line
                        LinkedList<int> horizontal_adjacent_pixel = coord_line[y*screenHeight+(x+x_dirmod)]; // The first four lines get the line data for the adjacent pixels
                        if (!IsDisplayPixelBlack(x, y+y_dirmod) && !IsDisplayPixelBlack(x+x_dirmod, y)) {
                            mirror_hit = new Vector2(x, y+y_dirmod); // This chooses only one of the adjacent pixels
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

    protected float findLineGradient(Vector2 mirror_hit, int regression_pixel_count) {
        int x = (int)mirror_hit.X;
        int y = (int)mirror_hit.Y;

        // Find line that was hit
        LinkedList<int> line_ids = coord_line[y*screenWidth + x];
        int line_id = line_ids.First.Value;
        LinkedList<Vector2> line = line_data[line_id];

        float line_gradient = 0;
        Vector2 point_to_be_found = new Vector2(x, y);

        // Find the regression if the line length is less than a specified hyperparameter
        // TO IMPLEMENT: two hyperparameters. My theory is that a large hyperparameter avoids overfitting (will not focus on noise) but will be inaccurate (in some cases, to the point that the reflection will go through the line). In order to prevent this, the regression formed with using more pixels will be compared to a regression formed with less pixels. From there on, idk. Compare them in some way, if they're too different, use the regression formed using the smaller hyperparameter
        // Probs best to put the regression calculation into a function taking the arguments: the line the pixel is on, position of the hit pixel and the number of pixels used in regression
        if (line.Count <= regression_pixel_count) { line_gradient = linearRegression(line); }
        else {
            // Find the regression if the line length is more than 5. This is done in the following manner:
            Vector2[] points = new Vector2[regression_pixel_count];
            LinkedListNode<Vector2> point_on_line = line.First;

            while (point_on_line.Next != null && points[(int)(regression_pixel_count/2)] != point_to_be_found) {
                for (int i=regression_pixel_count-2; i>=0; i--) { points[i+1] = points[i]; }
                points[0] = point_on_line.Value;
                point_on_line = point_on_line.Next;
            }

            LinkedList<Vector2> line_to_linearly_regress = new LinkedList<Vector2>();
            for (int j=0; j<regression_pixel_count; j++) { line_to_linearly_regress.AddLast(points[j]); }
            line_gradient = linearRegression(line_to_linearly_regress);
        }

        return line_gradient;
    }
    protected float linearRegression(LinkedList<Vector2> points) {
        int n = points.Count;
        float sum_x = 0;
        float sum_y = 0;
        float sum_x_y = 0;
        float sum_x_squared = 0;

        LinkedListNode<Vector2> point = points.First;
        while (point != null) {
            sum_x += point.Value.X;
            sum_y += point.Value.Y;
            sum_x_y += point.Value.X * point.Value.Y;
            sum_x_squared += point.Value.X * point.Value.X;
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
