using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Raven.Client.Documents.Attachments;
using Raven.Client.Documents.Operations.ETL;
using Raven.Client.Documents.Operations.ETL.SQL;
using Raven.Server.Documents.Patch;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;

namespace Raven.Server.Documents.ETL.Providers.SQL
{
    internal class SqlDocumentTransformer : EtlTransformer<ToSqlItem, SqlTableWithRecords>
    {
        private static readonly JsValue DefaultVarCharSize = 50;
        
        private readonly Transformation _transformation;
        private readonly SqlEtlConfiguration _config;
        private readonly Dictionary<string, SqlTableWithRecords> _tables;

        public SqlDocumentTransformer(Transformation transformation, DocumentDatabase database, DocumentsOperationContext context, SqlEtlConfiguration config)
            : base(database, context, new PatchRequest(transformation.Script, PatchRequestType.SqlEtl))
        {
            _transformation = transformation;
            _config = config;
            _tables = new Dictionary<string, SqlTableWithRecords>(_config.SqlTables.Count);

            var tables = new string[config.SqlTables.Count];

            for (var i = 0; i < config.SqlTables.Count; i++)
            {
                tables[i] = config.SqlTables[i].TableName;
            }

            LoadToDestinations = tables;
        }

        public override void Initalize()
        {
            base.Initalize();

            SingleRun.ScriptEngine.SetValue(Transformation.LoadAttachment, new ClrFunctionInstance(SingleRun.ScriptEngine, LoadAttachment));

            SingleRun.ScriptEngine.SetValue("varchar",
                new ClrFunctionInstance(SingleRun.ScriptEngine, (value, values) => ToVarcharTranslator(VarcharFunctionCall.AnsiStringType, values)));

            SingleRun.ScriptEngine.SetValue("nvarchar",
                new ClrFunctionInstance(SingleRun.ScriptEngine, (value, values) => ToVarcharTranslator(VarcharFunctionCall.StringType, values)));
        }

        protected override string[] LoadToDestinations { get; }

        protected override void LoadToFunction(string tableName, ScriptRunnerResult cols)
        {
            if (tableName == null)
                ThrowLoadParameterIsMandatory(nameof(tableName));

            var result = cols.TranslateToObject(Context);
            var columns = new List<SqlColumn>(result.Count);
            var prop = new BlittableJsonReaderObject.PropertyDetails();

            for (var i = 0; i < result.Count; i++)
            {
                result.GetPropertyByIndex(i, ref prop);

                var sqlColumn = new SqlColumn
                {
                    Id = prop.Name,
                    Value = prop.Value,
                    Type = prop.Token
                };

                if (_transformation.IsHandlingAttachments && 
                    prop.Token == BlittableJsonToken.String && IsLoadAttachment(prop.Value as LazyStringValue, out var attachmentName))
                {
                    var attachment = Database.DocumentsStorage.AttachmentsStorage.GetAttachment(Context, Current.DocumentId,
                        attachmentName, AttachmentType.Document, Current.Document.ChangeVector);
                    var attachmentStream = attachment?.Stream ?? Stream.Null;

                    sqlColumn.Type = 0;
                    sqlColumn.Value = attachmentStream;
                }

                columns.Add(sqlColumn);
            }

            GetOrAdd(tableName).Inserts.Add(new ToSqlItem(Current)
            {
                Columns = columns
            });
        }

        private JsValue LoadAttachment(JsValue self, JsValue[] args)
        {
            if (args.Length != 1 || args[0].IsString() == false)
                throw new InvalidOperationException($"{Transformation.AddAttachment}(name) must have a single string argument");

            return Transformation.AttachmentMarker + args[0].AsString();
        }

        private static unsafe bool IsLoadAttachment(LazyStringValue value, out string attachmentName)
        {
            if (value.Length <= Transformation.AttachmentMarker.Length)
            {
                attachmentName = null;
                return false;
            }

            var buffer = value.Buffer;

            if (*(long*)buffer != 7883660417928814884 || // $attachm
                *(int*)(buffer + 8) != 796159589) // ent/
            {
                attachmentName = null;
                return false;
            }

            attachmentName = value.Substring(Transformation.AttachmentMarker.Length);

            return true;
        }


        private SqlTableWithRecords GetOrAdd(string tableName)
        {
            if (_tables.TryGetValue(tableName, out SqlTableWithRecords table) == false)
            {
                _tables[tableName] =
                    table = new SqlTableWithRecords(_config.SqlTables.Find(x => x.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)));
            }

            return table;
        }

        public override IEnumerable<SqlTableWithRecords> GetTransformedResults()
        {
            return _tables.Values;
        }

        public override void Transform(ToSqlItem item)
        {
            if (item.IsDelete == false)
            {
                Current = item;

                SingleRun.Run(Context, Context, "execute", new object[] { Current.Document }).Dispose();
            }

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < _config.SqlTables.Count; i++)
            {
                // delete all the rows that might already exist there

                var sqlTable = _config.SqlTables[i];

                if (sqlTable.InsertOnlyMode)
                    continue;

                GetOrAdd(sqlTable.TableName).Deletes.Add(item);
            }
        }

        private JsValue ToVarcharTranslator(JsValue type, JsValue[] args)
        {
            if (args[0].IsString() == false)
                throw new InvalidOperationException("varchar() / nvarchar(): first argument must be a string");

            var sizeSpecified = args.Length > 1;

            if (sizeSpecified && args[1].IsNumber() == false)
                throw new InvalidOperationException("varchar() / nvarchar(): second argument must be a number");

            var item = SingleRun.ScriptEngine.Object.Construct(Arguments.Empty);

            item.Put(nameof(VarcharFunctionCall.Type), type, true);
            item.Put(nameof(VarcharFunctionCall.Value), args[0], true);
            item.Put(nameof(VarcharFunctionCall.Size), sizeSpecified ? args[1] : DefaultVarCharSize, true);

            return item;
        }

        public class VarcharFunctionCall
        {
            public static JsValue AnsiStringType = DbType.AnsiString.ToString();
            public static JsValue StringType = DbType.String.ToString();

            public DbType Type { get; set; }
            public object Value { get; set; }
            public int Size { get; set; }

            private VarcharFunctionCall()
            {

            }
        }
    }
}
