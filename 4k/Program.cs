using Raylib_cs;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

class Program
{
    const int windowWidth = 1280, windowHeight = 960;
    const string windowTitle = "4k";
    const int gameFPS = 120;
    static Color BLACK = new Color(0, 0, 0);
    static Color WHITE = new Color(255, 255, 255);

    static Texture2D trailTexture = new Texture2D();
    static Texture2D trail_pressedTexture = new Texture2D();
    static Texture2D tapTexture = new Texture2D();
    static Texture2D maxTexture = new Texture2D();
    static Texture2D greatTexture = new Texture2D();
    static Texture2D missTexture = new Texture2D();
    static Texture2D hold_headTexture = new Texture2D();
    static Texture2D hold_bodyTexture = new Texture2D();
    static Texture2D hold_tailTexture = new Texture2D();
    static Texture2D hold_short_bottomTexture = new Texture2D();
    static Texture2D trail_pressed_bottomTexture = new Texture2D();

    static Font font = new Font();

    static Music gameMusic = new Music();

    static List<Note> chart = new List<Note>();

    static bool[] pressed = new bool[4];
    static bool[] last_pressed = new bool[4];

    static double startTime;

    static string current_judge = "";
    static double display_judge_time = 0;
    static Color display_judge_mix_color = WHITE;

    static double scrolling_speed = 1500;

    static double accuracy = 100;
    static int combo = 0;
    static int total_combo = 0;
    static int max_cnt = 0;
    static int miss_cnt = 0;
    static int great_cnt = 0;
    public static void Main()
    {
        Raylib.InitWindow(windowWidth, windowHeight, windowTitle);
        Raylib.InitAudioDevice();

        Raylib.SetTargetFPS(gameFPS);

        LoadImage();
        LoadMusic();
        ReadChart();
        ReadSettings();

        font = Raylib.LoadFontEx("./resources/consola.ttf", 32, null, 0);

        Raylib.PlayMusicStream(gameMusic);

        bool running = true;

        startTime = Raylib.GetTime();

        while (running)
        {
            double current_time = Raylib.GetTime() - startTime;

            Raylib.UpdateMusicStream(gameMusic);

            if (Raylib.WindowShouldClose())
            {
                running = false;
            }

            pressed[0] = Raylib.IsKeyDown(KeyboardKey.D);
            pressed[1] = Raylib.IsKeyDown(KeyboardKey.F);
            pressed[2] = Raylib.IsKeyDown(KeyboardKey.J);
            pressed[3] = Raylib.IsKeyDown(KeyboardKey.K);

            Raylib.ClearBackground(BLACK);
            Raylib.BeginDrawing();

            judgeNotes(current_time);

            //轨道texture显示
            for (int i = 1; i <= 4; i++)
            {
                int xPos = windowWidth / 2 - 2 * trailTexture.Width + trailTexture.Width * (i - 1);
                if (pressed[i - 1]) Raylib.DrawTexture(trail_pressedTexture, xPos, 0, WHITE);
                else Raylib.DrawTexture(trailTexture, xPos, 0, WHITE);
            }

            //note移动
            foreach (Note current_note in chart)
            {
                int xPos = windowWidth / 2 - 2 * trailTexture.Width + trailTexture.Width * (current_note.position - 1);
                switch (current_note.type)
                {
                    case "tap":
                        int yPos = Convert.ToInt32(windowHeight - trail_pressed_bottomTexture.Height / 2 + scrolling_speed * (current_time - current_note.time)) + 5;
                        if (yPos >= 0 && yPos < windowHeight) Raylib.DrawTexture(tapTexture, xPos, yPos, WHITE);
                        break;

                    case "hold":
                        //hold足够长
                        if(current_note.hold_length > 0)
                        {
                            //头部
                            int head_yPos = Convert.ToInt32(windowHeight - trail_pressed_bottomTexture.Height / 2 + scrolling_speed * (current_time - current_note.time));
                            if (head_yPos >= -hold_headTexture.Height && head_yPos < windowHeight) Raylib.DrawTexture(hold_headTexture, xPos, head_yPos, WHITE);
                            //身体
                            for (int i = 1; i < current_note.hold_length; i++)
                            {
                                int body_yPos = head_yPos - i * hold_bodyTexture.Height;
                                if (body_yPos >= -hold_bodyTexture.Height && body_yPos < windowHeight) Raylib.DrawTexture(hold_bodyTexture, xPos, body_yPos, WHITE);
                            }
                            //尾巴
                            double rest_time = current_note.hold_duration - current_note.hold_length * hold_bodyTexture.Height / scrolling_speed;
                            int tail_height = Convert.ToInt32(rest_time * scrolling_speed);
                            Rectangle tail_rect = new Rectangle(0, 0, hold_tailTexture.Width, tail_height);
                            int tail_yPos = head_yPos - hold_bodyTexture.Height * (current_note.hold_length - 1) - tail_height;
                            if (tail_yPos >= -tail_height && tail_yPos < windowHeight) Raylib.DrawTextureRec(hold_tailTexture, tail_rect, new System.Numerics.Vector2(xPos, tail_yPos), WHITE);
                        }
                        //hold太短
                        else if(current_note.hold_length == 0)
                        {
                            int height = Convert.ToInt32(current_note.hold_duration * scrolling_speed);
                            Rectangle rect = new Rectangle(0, 0, hold_tailTexture.Width, height);
                            int yPos_ = Convert.ToInt32(windowHeight - trail_pressed_bottomTexture.Height / 2 + scrolling_speed * (current_time - current_note.time)) + (hold_tailTexture.Height - height);
                            if(yPos_ >= -height && yPos_ < windowHeight)
                            {
                                Raylib.DrawTextureRec(hold_tailTexture, rect, new System.Numerics.Vector2(xPos, yPos_), WHITE);
                                Raylib.DrawTexture(hold_short_bottomTexture, xPos, yPos_ + height - hold_short_bottomTexture.Height, WHITE);
                            }
                        }
                        //蒙版
                        if (current_note.hold_judging)
                        {
                            Raylib.DrawTexture(trail_pressed_bottomTexture, xPos, windowHeight - trail_pressed_bottomTexture.Height, WHITE);
                        }
                        break;
                }
            }

            //判定文字texture显示
            double display_judge_last_time = current_time - display_judge_time;

            if (display_judge_last_time <= 0.8)
            {
                display_judge_mix_color = new Color(Convert.ToInt32(255 * (1 - Math.Pow(display_judge_last_time, 3))), Convert.ToInt32(255 * (1 - Math.Pow(display_judge_last_time, 3))), Convert.ToInt32(255 * (1 - Math.Pow(display_judge_last_time, 3))));
                switch (current_judge)
                {
                    case "max":
                        Raylib.DrawTexture(maxTexture, (windowWidth - maxTexture.Width) / 2, (windowHeight - maxTexture.Height) / 2, display_judge_mix_color);
                        break;

                    case "great":
                        Raylib.DrawTexture(greatTexture, (windowWidth - greatTexture.Width) / 2, (windowHeight - greatTexture.Height) / 2, display_judge_mix_color);
                        break;

                    case "miss":
                        Raylib.DrawTexture(missTexture, (windowWidth - missTexture.Width) / 2, (windowHeight - missTexture.Height) / 2, display_judge_mix_color);
                        break;
                }
            }

            //combo&acc显示
            Vector2 combo_position = new Vector2(0, 0);
            Vector2 acc_position = new Vector2(0, 32);
            string combo_str = combo.ToString("F0");
            string acc_str = accuracy.ToString("F2");
            byte[] combo_bytes = Encoding.UTF8.GetBytes(combo_str + '\0');
            byte[] acc_bytes = Encoding.UTF8.GetBytes(acc_str + '\0');
            unsafe
            {
                fixed(byte* ptr = combo_bytes)
                {
                    sbyte* ptr2 = (sbyte*)ptr;
                    Raylib.DrawTextEx(font, ptr2, combo_position, 32, 2, WHITE);
                }
                fixed (byte* ptr = acc_bytes)
                {
                    sbyte* ptr2 = (sbyte*)ptr;
                    Raylib.DrawTextEx(font, ptr2, acc_position, 32, 2, WHITE);
                }
            }

            Raylib.EndDrawing();

            for (int i = 0; i < 4; i++) last_pressed[i] = pressed[i];

            chart.RemoveAll(note => note.judged == true);
        }

        Raylib.CloseWindow();
        Raylib.CloseAudioDevice();
    }

    public static void LoadImage()
    {
        trailTexture = Raylib.LoadTexture("./resources/trail.png");
        trail_pressedTexture = Raylib.LoadTexture("./resources/trail_pressed.png");
        tapTexture = Raylib.LoadTexture("./resources/tap.png");
        maxTexture = Raylib.LoadTexture("./resources/max.png");
        greatTexture = Raylib.LoadTexture("./resources/great.png");
        missTexture = Raylib.LoadTexture("./resources/miss.png");
        hold_headTexture = Raylib.LoadTexture("./resources/hold_head.png");
        hold_bodyTexture = Raylib.LoadTexture("./resources/hold_body.png");
        hold_tailTexture = Raylib.LoadTexture("./resources/hold_tail.png");
        hold_short_bottomTexture = Raylib.LoadTexture("./resources/hold_short_bottom.png");
        trail_pressed_bottomTexture = Raylib.LoadTexture("./resources/trail_pressed_bottom.png");
    }

    public static void LoadMusic()
    {
        gameMusic = Raylib.LoadMusicStream("./resources/track.mp3");
    }

    public static void judgeNotes(double current_time)
    {
        for(int trail = 1; trail <= 4; trail++)
        {
            bool judge_over_flag = false;
            foreach (Note current_note in chart)
            {
                if (current_note.position != trail) continue;
                if (current_note.type == "tap")
                {
                    double delta_time = current_time - current_note.time;
                    //判定tap
                    if (!last_pressed[trail - 1] && pressed[trail - 1])
                    {
                        if (Math.Abs(delta_time) <= 0.04)
                        {
                            current_judge = "max";
                            current_note.judged = true;
                            display_judge_time = current_time;
                            judge_over_flag = true;
                            combo++;
                            total_combo++;
                            max_cnt++;
                        }
                        if (Math.Abs(delta_time) <= 0.08 && Math.Abs(delta_time) > 0.04)
                        {
                            current_judge = "great";
                            current_note.judged = true;
                            display_judge_time = current_time;
                            judge_over_flag = true;
                            combo++;
                            total_combo++;
                            great_cnt++;
                        }
                    }
                    if (delta_time > 0.08 && !current_note.judged)
                    {
                        current_judge = "miss";
                        display_judge_time = current_time;
                        current_note.judged = true;
                        combo = 0;
                        total_combo++;
                        miss_cnt++;
                    }
                }
                if (current_note.type == "hold")
                {
                    //判定hold
                    //身体&尾
                    double delta_time = current_time - current_note.time;
                    if (current_note.hold_head_judged)
                    {
                        if(current_time <= current_note.time + current_note.hold_duration)
                        {
                            if (!(last_pressed[trail - 1] && pressed[trail - 1]) && (current_note.time + current_note.hold_duration - current_time >= 0.08))
                            {
                                current_judge = "miss";
                                display_judge_time = current_time;
                                current_note.judged = true;
                                current_note.hold_judging = false;
                                combo = 0;
                                total_combo++;
                                miss_cnt++;
                            }
                        }
                        else
                        {
                            current_note.judged = true;
                            current_note.hold_judging = false;
                            judge_over_flag = true;
                        }
                    }
                    //头
                    else
                    {
                        if (!last_pressed[trail - 1] && pressed[trail - 1])
                        {
                            if (delta_time >= -0.04 && delta_time <= 0.04)
                            {
                                current_judge = "max";
                                display_judge_time = current_time;
                                current_note.hold_judging = true;
                                current_note.hold_head_judged = true;
                                judge_over_flag = true;
                                combo++;
                                total_combo++;
                                max_cnt++;
                            }
                            if ((delta_time >= -0.08 && delta_time <= -0.04) || (delta_time >= 0.04 && delta_time <= 0.08))
                            {
                                current_judge = "great";
                                display_judge_time = current_time;
                                current_note.hold_judging = true;
                                current_note.hold_head_judged = true;
                                judge_over_flag = true;
                                combo++;
                                total_combo++;
                                great_cnt++;
                            }
                        }
                        if (delta_time > 0.08)
                        {
                            current_judge = "miss";
                            display_judge_time = current_time;
                            current_note.judged = true;
                            current_note.hold_head_judged = true;
                            current_note.hold_judging = false;
                            combo = 0;
                            total_combo++;
                            miss_cnt++;
                        }
                    }
                }
                if (judge_over_flag) break;
            }
        }
        accuracy = calcAcc();
    }
    
    public static void ReadChart()
    {
        double bpm = 120;
        double beat = 4;
        double current_time = 0;
        StreamReader reader = new StreamReader("./resources/chart.txt");
        string data = "";
        while((data = reader.ReadLine()!) != null)
        {
            if (data[0] == '&')
            {
                string meta_data = data.Substring(1);
                string[] meta_data_splited = meta_data.Split(':');
                string key = meta_data_splited[0];
                string value = meta_data_splited[1];
                switch (key)
                {
                    case "offset":
                        current_time = Convert.ToDouble(value);
                        break;

                    case "bpm":
                        bpm = Convert.ToDouble(value);
                        break;

                    case "beat":
                        beat = Convert.ToDouble(value);
                        break;
                }
            }
            else
            {
                string[] note_strings = data.Split(',');
                int idx = 0;
                foreach(string note_string in note_strings)
                {
                    if (idx == note_strings.Count() - 1) break;
                    if(note_string == "")
                    {
                        current_time += 60.0 / bpm / beat * 4.0;
                        idx++;
                        continue;
                    }
                    if (note_string.Contains('+'))
                    {
                        string[] single_note_strings = note_string.Split('+');
                        foreach(string single_note_string in single_note_strings)
                        {
                            if (single_note_string.Contains('h'))
                            {
                                Note note = new Note();
                                note.time = current_time;
                                note.position = Convert.ToInt32(single_note_string.Split('h')[0]);
                                string holding_time_string = single_note_string.Split('h')[1];
                                double holding_beat = Convert.ToDouble(holding_time_string.Split(':')[0]);
                                double holding_scale = Convert.ToDouble(holding_time_string.Split(":")[1]);
                                note.hold_duration = 60.0 / bpm / holding_beat * 4.0 * holding_scale;
                                note.calcHoldLength(scrolling_speed, hold_bodyTexture.Height);
                                note.type = "hold";
                                chart.Add(note);
                            }
                            else
                            {
                                Note note = new Note();
                                note.time = current_time;
                                note.position = Convert.ToInt32(single_note_string);
                                note.type = "tap";
                                chart.Add(note);
                            }
                        }
                    }
                    else
                    {
                        if (note_string.Contains('h'))
                        {
                            Note note = new Note();
                            note.time = current_time;
                            note.position = Convert.ToInt32(note_string.Split('h')[0]);
                            string holding_time_string = note_string.Split('h')[1];
                            double holding_beat = Convert.ToDouble(holding_time_string.Split(':')[0]);
                            double holding_scale = Convert.ToDouble(holding_time_string.Split(":")[1]);
                            note.hold_duration = 60.0 / bpm / holding_beat * 4.0 * holding_scale;
                            note.calcHoldLength(scrolling_speed, hold_bodyTexture.Height);
                            note.type = "hold";
                            chart.Add(note);
                        }
                        else
                        {
                            Note note = new Note();
                            note.time = current_time;
                            note.position = Convert.ToInt32(note_string);
                            note.type = "tap";
                            chart.Add(note);
                        }
                    }
                    current_time += 60.0 / bpm / beat * 4.0;
                    idx++;
                }
            }
        }
        reader.Close();
    }

    public static double calcAcc()
    {
        if (total_combo == 0) return 0;
        double acc;
        acc = 100 * (max_cnt + great_cnt * 0.75) / total_combo;
        return acc;
    }

    public static void ReadSettings()
    {
        string jsonString = File.ReadAllText("./resources/settings.json");
        Console.WriteLine($"{jsonString}");
        Settings? settings = JsonSerializer.Deserialize<Settings>(jsonString);
        if (settings == null) return;
        Console.WriteLine($"{settings.scrolling_speed}, {settings.input_offset}");
        scrolling_speed = settings.scrolling_speed;
        foreach(Note note in chart)
        {
            note.time += settings.input_offset / 1000.0;
        }
    }
}

public class Note
{
    public string type = "";
    public double time;
    public int position;
    public bool judged = false;

    public double hold_duration;
    public bool hold_judging = false;
    public bool hold_head_judged = false;
    public int hold_length;

    public void calcHoldLength(double scrolling_speed, int texture_height)
    {
        hold_length = Convert.ToInt32(Math.Ceiling(hold_duration * scrolling_speed / texture_height)) - 1;
    }
}

public class Settings
{
    [JsonPropertyName("scrolling_speed")]
    public double scrolling_speed{ get; set; }
    [JsonPropertyName("input_offset")]
    public double input_offset { get; set; }
}