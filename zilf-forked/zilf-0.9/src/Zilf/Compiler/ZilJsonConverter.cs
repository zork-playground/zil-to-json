using Zilf.Compiler;
using Newtonsoft.Json;
using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Zilf.ZModel.Values;
using Zilf.Interpreter.Values;
using System.Reflection;
using Zilf.Interpreter;
using Zilf.Language;

public class ZilJsonConverter : JsonConverter

{
    private static ZilObject FALSE = new ZilFalse(new ZilList(null, null));
    private List<String> directionNames;
    private bool expandTables;
    private string ATOM = "A";
    private string FORM = "F";

    public ZilJsonConverter(List<String> directionNames, bool expandTables)
    {
        this.directionNames = directionNames;
        this.expandTables = expandTables;
    }

    static Boolean isRoom(ZilModelObject obj)
    {
        bool isRoom = obj.IsRoom;
        if (isRoom)
        {
            return true;
        }
        foreach (Zilf.Interpreter.Values.ZilList propList in obj.Properties)
        {
            Zilf.Interpreter.Values.ZilObject propNameObject = propList[0];
            if (propNameObject is Zilf.Interpreter.Values.ZilAtom propNameAtom)
            {
                string propName = propNameAtom.Text;
                if (propName.Equals("IN"))
                {
                    Zilf.Interpreter.Values.ZilAtom singleValue = (Zilf.Interpreter.Values.ZilAtom)propList[1];
                    if (singleValue.Text.Equals("ROOMS") && !isRoom)
                    {
                        Console.WriteLine("WARNING: ZilModelObject.IsRoom was false but (IN ROOMS); treating like a room: " + obj.Name.Text);
                        isRoom = true;
                        return true;
                    }
                }
            }
        }
        return false;
    }


    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        bool includeRawForms = true;
        if (value is Zilf.ZModel.ZEnvironment ZEnv)
        {
            JObject all = new JObject();
            //all.Add("Objects", JToken.FromObject((ZEnv.Objects.Where((_o) => !isRoom(_o)).ToList(), serializer)));
            //all.Add("Rooms", JToken.FromObject((ZEnv.Objects.Where((_o) => isRoom(_o)).ToList(), serializer)));
            all.Add("Objects", JToken.FromObject(ZEnv.Objects, serializer));
            all.Add("Directions", JToken.FromObject(ZEnv.Directions, serializer));
            all.Add("Globals", JToken.FromObject(ZEnv.Globals, serializer));
            all.Add("Routines", JToken.FromObject(ZEnv.Routines, serializer));
            all.Add("Constants", JToken.FromObject(ZEnv.Constants, serializer));
            all.Add("PropertyDefaults", JToken.FromObject(ZEnv.PropertyDefaults, serializer));
            all.Add("Synonyms", JToken.FromObject(ZEnv.Synonyms, serializer));
            all.Add("Syntaxes", JToken.FromObject(ZEnv.Syntaxes, serializer));
            this.expandTables = true;
            all.Add("Tables", JToken.FromObject(ZEnv.Tables, serializer));
            this.expandTables = false;
            all.Add("Vocabulary", JToken.FromObject(ZEnv.Vocabulary, serializer));
            all.Add("Buzzwords", JToken.FromObject(ZEnv.Buzzwords, serializer));
            all.WriteTo(writer);
        }
        else if (value is ZilAtom atom)
        {
            JObject o = new JObject();
            o.Add(ATOM, atom.Text);
            o.WriteTo(writer);
        }
        else if (value is List<ZilAtom> atomList)
        {
            // Ex: Directions
            JArray array = new JArray();
            foreach (ZilAtom child in atomList)
            {
                array.Add(JToken.FromObject(child.Text, serializer));
            }
            array.WriteTo(writer);
        }
        else if (value is ZilModelObject obj)
        {
            JObject jProps = new JObject();
            bool hasAddedLocation = false;
            JObject exits = new JObject();
            // NOTE: For most games, obj.IsRoom seems to be correct, but
            // for wishbringer, this is always false for some reason.
            // So we will change isRoom to true later below if we
            // find that it's IN "ROOMS".
            Boolean isRoom = obj.IsRoom;
            foreach (ZilList propList in obj.Properties)
            {
                ZilObject propNameObject = propList[0];
                string propName = "";
                if (propNameObject is ZilAtom propNameAtom)
                {
                    propName = propNameAtom.Text;
                } else
                {
                    propName = "TODO";
                }
                ZilListoidBase rest = propList.GetRest(1);
                bool hasTranslatedValue = false;
                if (propName.Equals("IN"))
                {
                    if (!hasAddedLocation)
                    {
                        propName = "#IN";
                        hasAddedLocation = true;
                        ZilAtom singleValue = (ZilAtom)propList[1];
                        jProps.Add("#IN", singleValue.Text);
                        if (singleValue.Text.Equals("ROOMS") && !isRoom)
                        {
                            Console.WriteLine("WARNING: obj.IsRoom was false but found (IN ROOMS); setting isRoom to true");
                            isRoom = true;
                        }
                        hasTranslatedValue = true;
                    }
                }
                if (propName.Equals("SYNONYM") || propName.Equals("ADJECTIVE") || propName.Equals("FLAGS"))
                {
                    // always a list of atoms
                    JArray stringArray = new JArray();
                    for (int i=1; i<propList.GetLength(); i++)
                    {
                        ZilAtom ithAtom = (ZilAtom)propList[i];
                        stringArray.Add(ithAtom.Text);
                    }
                    jProps.Add(propName, stringArray);
                    hasTranslatedValue = true;
                }
                if (!hasTranslatedValue)
                {
                    jProps.Add(propName, JToken.FromObject(rest, serializer));
                }
                if (isRoom && directionNames.Contains(propName))
                {
                    string exitType = "#UNKNOWN";
                    JObject exit = new JObject();
                    ZilObject o1 = propList[1];
                    if (o1 is ZilAtom atom1)
                    {
                        if (atom1.Text.Equals("TO"))
                        {
                            exit.Add("TO", propList[2].ToString());
                            if (propList.GetLength() == 3)
                            {
                                // unconditional exit
                                exitType = "UEXIT";
                            }
                            else
                            {
                                ZilObject o3 = propList[3];
                                if (o3 is ZilAtom atom3 && atom3.Text.Equals("IF"))
                                {
                                    ZilObject o4 = propList[4];
                                    if (propList.GetLength() == 5)
                                    {
                                        // conditional exit without custom message
                                        exitType = "CEXIT";
                                        exit.Add("COND", o4.ToString());
                                    }
                                    else if (propList.GetLength() >= 7)
                                    {
                                        ZilObject o5 = propList[5];
                                        ZilObject o6 = propList[6];
                                        ZilAtom atom5 = (ZilAtom)o5;
                                        if (atom5.Text.Equals("ELSE"))
                                        {
                                            // conditional exit with custom message
                                            exitType = "CEXIT";
                                            exit.Add("COND", o4.ToString());
                                            exit.Add("ELSE", ((ZilString)o6).Text);
                                        }
                                        else if (atom5.Text.Equals("IS") && o6 is ZilAtom atom6 && atom6.Text.Equals("OPEN"))
                                        {
                                            // door exit
                                            exitType = "DEXIT";
                                            exit.Add("DOOR", o4.ToString());
                                            if (propList.GetLength() >= 9)
                                            {
                                                ZilObject o7 = propList[7];
                                                ZilObject o8 = propList[8];
                                                if (o7 is ZilAtom atom7 && atom7.Text.Equals("ELSE"))
                                                {
                                                    if (o8 is ZilString s8)
                                                    {
                                                        exit.Add("ELSE", s8.Text);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (atom1.Text.Equals("PER"))
                        {
                            exit.Add("PER", propList[2].ToString());
                            exitType = "FEXIT";
                        }
                        else if (atom1.Text.Equals("SORRY"))
                        {
                            exit.Add("MESSAGE", ((ZilString)propList[2]).Text);
                            exitType = "NEXIT";
                        }
                    }
                    else if (o1 is ZilString s1)
                    {
                        exitType = "NEXIT";
                        exit.Add("MESSAGE", s1.Text);
                    }
                    exit.Add("TYPE", exitType);
                    ////exit.Add("Zil", propList.ToString());
                    exits.Add(propName, exit);
                }
            }
            JObject o = new JObject();
            ////o.Add("Zil", JToken.FromObject(obj.ToString(), serializer));
            o.Add("Name", JToken.FromObject(obj.Name.Text, serializer));
            if (isRoom)
            {
                o.Add("Exits", exits);
            }
            o.Add("Properties", jProps);
            o.Add("IsRoom", isRoom);
            o.WriteTo(writer);
        }
        else if (value is List<ZilModelObject> objList)
        {
            JArray array = new JArray();
            foreach (ZilModelObject child in objList)
            {
                array.Add(JToken.FromObject(child, serializer));
            }
            array.WriteTo(writer);
        }
        else if (value is ZilRoutine zilRoutine)
        {
            JObject o = new JObject();
            o.Add("Source", JToken.FromObject(GetShortFileName(zilRoutine.SourceLine), serializer));
            o.Add("Name", zilRoutine.Name.Text);
            o.Add("ArgSpec", JToken.FromObject(zilRoutine.ArgSpec.ToString(), serializer)); // TODO: Expand ArgSpec
            string saveATOM = ATOM;
            string saveFORM = FORM;
            ATOM = "A";
            FORM = "F";
            o.Add("Body", JToken.FromObject(zilRoutine.Body, serializer));
            ATOM = saveATOM;
            FORM = saveFORM;
            o.WriteTo(writer);
        }
        else if (value is List<ZilRoutine> routineList)
        {
            JArray array = new JArray();
            foreach (ZilRoutine routine in routineList)
            {
                array.Add(JToken.FromObject(routine, serializer));
            }
            array.WriteTo(writer);
        }
        //else if (value is ZilDecl zilDecl)
        //{
        //    JToken.FromObject(zilDecl).WriteTo(writer);
        //}
        else if (value is ZilFalse zilFalse)
        {
            JToken t = JToken.FromObject(false);
            t.WriteTo(writer);
        }
        else if (value is ZilConstant zilConstant)
        {
            JObject o = new JObject();
            o.Add(zilConstant.Name.Text, JToken.FromObject(zilConstant.Value, serializer));
            o.WriteTo(writer);
        }
        else if (value is List<ZilConstant> constantList)
        {
            JObject o = new JObject();
            foreach (ZilConstant constant in constantList)
            {
                o.Add(constant.Name.Text, JToken.FromObject(constant.Value, serializer));
            }
            o.WriteTo(writer);
        }
        else if (value is List<ZilGlobal> globalList)
        {
            JObject o = new JObject();
            foreach (ZilGlobal global in globalList)
            {
                if (global.Value is ZilTable globalTable)
                {
                    JObject tableRef = new JObject();
                    tableRef.Add("T", globalTable.Name);
                    o.Add(global.Name.Text, tableRef);
                }
                else
                {
                    o.Add(global.Name.Text, JToken.FromObject(global.Value, serializer));
                }
            }
            o.WriteTo(writer);
        }
        else if (value is Zilf.ZModel.Vocab.PartOfSpeech pos)
        {
            JToken t = JToken.FromObject(pos.ToString(), serializer);
            t.WriteTo(writer);
        }
        else if (value is Zilf.ZModel.Vocab.OldParser.OldParserWord oldParserWord)
        {
            JObject o = new JObject();
            o.Add(ATOM, JToken.FromObject(oldParserWord.Atom, serializer));
            o.Add("PartOfSpeech", JToken.FromObject(oldParserWord.PartOfSpeech, serializer));
            o.Add("SynonymTypes", JToken.FromObject(oldParserWord.SynonymTypes, serializer));
            o.WriteTo(writer);
        }
        else if (value is Zilf.ZModel.Vocab.Synonym synonym)
        {
            JObject o = new JObject();
            o.Add(synonym.SynonymWord.Atom.Text, JToken.FromObject(synonym.OriginalWord.Atom.Text, serializer));
            o.WriteTo(writer);
        }
        else if (value is List<Zilf.ZModel.Vocab.Synonym> synonymList)
        {
            Dictionary<string, List<string>> synonymDict = new Dictionary<string, List<string>>();
            foreach (Zilf.ZModel.Vocab.Synonym child in synonymList)
            {
                //o.Add(child.SynonymWord.Atom.Text, JToken.FromObject(child.OriginalWord.Atom.Text, serializer));
                string originalWord = child.OriginalWord.Atom.Text;
                string synonymWord = child.SynonymWord.Atom.Text;
                if (!synonymDict.ContainsKey(originalWord))
                {
                    synonymDict[originalWord] = new List<string>();
                }
                synonymDict[originalWord].Add(synonymWord);
            }
            JObject o = new JObject();
            foreach (KeyValuePair<string, List<string>> kvp in synonymDict)
            {
                o.Add(kvp.Key, new JArray(kvp.Value));
            }
            o.WriteTo(writer);
        }
        else if (value is Dictionary<ZilAtom, ZilObject> dictionary)
        {
            // Ex: PropertyDefaults
            JObject o = new JObject();
            foreach (ZilAtom key in dictionary.Keys)
            {
                ZilObject dictValue = dictionary[key];
                o.Add(key.Text, JToken.FromObject(dictValue, serializer));
            }
            o.WriteTo(writer);
        }
        else if (value is Dictionary<ZilAtom, Zilf.ZModel.Vocab.IWord> vocabDict)
        {
            JObject o = new JObject();
            foreach (ZilAtom key in vocabDict.Keys)
            {
                Zilf.ZModel.Vocab.IWord word = vocabDict[key];
                if (word is Zilf.ZModel.Vocab.OldParser.OldParserWord oldWord)
                {
                    o.Add(key.Text, oldWord.PartOfSpeech.ToString());
                }
                else
                {
                    o.Add(key.Text, JToken.FromObject(word, serializer));
                }
            }
            o.WriteTo(writer);
        }
        else if (value is List<KeyValuePair<ZilAtom, ISourceLine>> buzzwordList) {
            // Buzzwords
            JArray array = new JArray();
            foreach (KeyValuePair<ZilAtom, ISourceLine> pair in buzzwordList)
            {
                array.Add(pair.Key.Text);
            }
            array.WriteTo(writer);
        }
        else if (value is Zilf.ZModel.Syntax syntax)
        {
            JObject o = new JObject();
            ////o.Add("Syntax", syntax.ToString());
            o.Add("NumObjects", syntax.NumObjects);
            o.Add("Verb", syntax.Verb.Atom.Text);
            if (syntax.NumObjects >= 1)
            {
                o.Add("Preposition1", syntax.Preposition1?.Atom.Text);
                o.Add("Object1", "OBJECT");
                o.Add("FindFlags1", syntax.FindFlag1?.ToString());
                o.Add("ScopeFlags1", ToScopeFlagsString(syntax.Options1));
            }
            if (syntax.NumObjects >= 2)
            {
                o.Add("Preposition2", syntax.Preposition2?.Atom.Text);
                o.Add("Object2", "OBJECT");
                o.Add("FindFlags2", syntax.FindFlag2?.ToString());
                o.Add("ScopeFlags2", ToScopeFlagsString(syntax.Options2));
            }
            o.Add("Action", syntax.Action.Text);
            o.Add("Preaction", syntax.Preaction?.Text);
            o.WriteTo(writer);
        }
        else if (value is List<Zilf.ZModel.Syntax> syntaxList)
        {
            JArray array = new JArray();
            foreach (Zilf.ZModel.Syntax child in syntaxList)
            {
                array.Add(JToken.FromObject(child, serializer));
            }
            array.WriteTo(writer);
        }
        else if (value is ZilForm zilForm)
        {
            JArray array = new JArray();
            foreach (ZilObject zilObject2 in zilForm)
            {
                array.Add(JToken.FromObject(zilObject2, serializer));
            }
            if (includeRawForms)
            {
                JObject o = new JObject();
                ////o.Add("Zil", JToken.FromObject(zilForm.ToString(), serializer));
                o.Add(FORM, array);
                o.WriteTo(writer);
            } else
            {
                writer.WriteValue(zilForm.ToString());
            }
        }
        else if (value is ZilList zilList)
        {
            JArray array = new JArray();
            for (int i=0; i<zilList.GetLength(); i++)
            {
                ZilObject ithObj = zilList[i];
                array.Add(JToken.FromObject(ithObj, serializer));
            }
            array.WriteTo(writer);
        }
        else if (value is ZilList[] zilListArray)
        {
            // Ex: Object Properties
            JArray array = new JArray();
            foreach (ZilList zilList2 in zilListArray)
            {
                array.Add(JToken.FromObject(zilList2, serializer));
            }
            array.WriteTo(writer);
        }
        else if (value is ZilObject[] zilObjectArray)
        {
            JArray array = new JArray();
            foreach (ZilObject zilObject2 in zilObjectArray)
            {
                array.Add(JToken.FromObject(zilObject2, serializer));
            }
            array.WriteTo(writer);
        }
        //Zilf.ZModel.Values.TableFlags
        //else if (value is Zilf.ZModel.Values.TableFlags tableFlags)
        //{
        //    JObject o = new JObject();
        //    o.Add("T", JToken.FromObject(zilTable.ToString(), serializer));
        //    o.Add("Flags", JToken.FromObject(zilTable.Flags, serializer));
        //    o.WriteTo(writer);
        //}
        else if (value is ZilTable zilTable)
        {
            JObject o = new JObject();
            if (expandTables)
            {
                ////o.Add("Source", zilTable.SourceLine.SourceInfo);
                o.Add("Source", JToken.FromObject(GetShortFileName(zilTable.SourceLine), serializer));
                o.Add("Name", zilTable.Name);
                ////o.Add("Zil", JToken.FromObject(zilTable.ToString(), serializer));
                o.Add("Flags", JToken.FromObject(zilTable.Flags.ToString(), serializer));
                ZilObject[] array = new ZilObject[zilTable.ElementCount];
                Context dummyCtx = null;
                zilTable.CopyTo(array, (zo, isWord) => zo, FALSE, dummyCtx);
                o.Add("Data", JToken.FromObject(array, serializer));
            }
            else
            {
                o.Add("T", zilTable.Name);
            }
            o.WriteTo(writer);
        }
        else if (value is List<ZilTable> zilTableList)
        {
            JArray array = new JArray();
            foreach (ZilTable child in zilTableList)
            {
                array.Add(JToken.FromObject(child, serializer));
            }
            array.WriteTo(writer);
        }
        else if (value is ZilFix zilFix)
        {
            writer.WriteValue(zilFix.Value);
        }
        else if (value is ZilString zilString) // .GetType() == typeof(ZilString)
        {
            writer.WriteValue(zilString.Text);
        }
        else if (value.GetType().IsPrimitive || value.GetType() == typeof(String))
        {
            writer.WriteValue(value);
        }
        else
        {
            writer.WriteValue("[" + value.GetType() + "]");
        }
    }

    [Flags]
    enum ScopeFlag {
        HAVE = Zilf.ZModel.Vocab.ScopeFlags.Original.Have,
        MANY = Zilf.ZModel.Vocab.ScopeFlags.Original.Many,
        TAKE = Zilf.ZModel.Vocab.ScopeFlags.Original.Take,
        ON_GROUND = Zilf.ZModel.Vocab.ScopeFlags.Original.OnGround,
        IN_ROOM = Zilf.ZModel.Vocab.ScopeFlags.Original.InRoom,
        CARRIED = Zilf.ZModel.Vocab.ScopeFlags.Original.Carried,
        HELD = Zilf.ZModel.Vocab.ScopeFlags.Original.Held
    }

    public string ToScopeFlagsString(byte b)
    {
        List<String> flagNames = new List<String>();
        if ((b & Zilf.ZModel.Vocab.ScopeFlags.Original.Have) != 0) { flagNames.Add("HAVE"); }
        if ((b & Zilf.ZModel.Vocab.ScopeFlags.Original.Many) != 0) { flagNames.Add("MANY"); }
        if ((b & Zilf.ZModel.Vocab.ScopeFlags.Original.Take) != 0) { flagNames.Add("TAKE"); }
        if ((b & Zilf.ZModel.Vocab.ScopeFlags.Original.OnGround) != 0) { flagNames.Add("ON-GROUND"); }
        if ((b & Zilf.ZModel.Vocab.ScopeFlags.Original.InRoom) != 0) { flagNames.Add("IN-ROOM"); }
        if ((b & Zilf.ZModel.Vocab.ScopeFlags.Original.Carried) != 0) { flagNames.Add("CARRIED"); }
        if ((b & Zilf.ZModel.Vocab.ScopeFlags.Original.Held) != 0) { flagNames.Add("HELD"); }
        return String.Join(", ", flagNames);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
    }

    public override bool CanRead
    {
        get { return false; }
    }

    public override bool CanConvert(Type objectType)
    {
        /*
        if (objectType == typeof(Zilf.ZModel.ZEnvironment))
        {
            return true;
        }
        return false;
        */
        return true; // _types.Any(t => t == objectType);
    }

    static string GetShortFileName(ISourceLine sourceLine)
    {
        string longFilename = sourceLine.SourceInfo;
        return longFilename.Substring(longFilename.LastIndexOf("\\") + 1);
    }

}
