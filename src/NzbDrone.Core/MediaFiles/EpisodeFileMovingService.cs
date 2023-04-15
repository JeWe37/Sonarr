using System.IO;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles
{
    public interface IMoveEpisodeFiles
    {
        EpisodeFile MoveEpisodeFile(EpisodeFile episodeFile, LocalEpisode localEpisode, ScriptImportDecisionInfo scriptImportDecisionInfo);
        EpisodeFile CopyEpisodeFile(EpisodeFile episodeFile, LocalEpisode localEpisode, ScriptImportDecisionInfo scriptImportDecisionInfo);
    }

    public class EpisodeFileMovingService : EpisodeFileTransferringService, IMoveEpisodeFiles
    {
        private readonly IUpdateMediaInfo _updateMediaInfo;
        private readonly IDiskTransferService _diskTransferService;
        private readonly IScriptImportDecider _scriptImportDecider;
        private readonly IRenameEpisodeFiles _renameEpisodeFiles;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public EpisodeFileMovingService(IUpdateEpisodeFileService updateEpisodeFileService,
                                IUpdateMediaInfo updateMediaInfo,
                                IBuildFileNames buildFileNames,
                                IDiskTransferService diskTransferService,
                                IDiskProvider diskProvider,
                                IMediaFileAttributeService mediaFileAttributeService,
                                IScriptImportDecider scriptImportDecider,
                                IRenameEpisodeFiles renameEpisodeFiles,
                                IEventAggregator eventAggregator,
                                IConfigService configService,
                                Logger logger)
            : base(updateEpisodeFileService, buildFileNames, diskProvider, mediaFileAttributeService, eventAggregator, logger)
        {
            _updateMediaInfo = updateMediaInfo;
            _diskTransferService = diskTransferService;
            _scriptImportDecider = scriptImportDecider;
            _renameEpisodeFiles = renameEpisodeFiles;
            _configService = configService;
            _logger = logger;
        }

        protected override void ExecuteTransfer(string episodeFilePath, string destinationFilePath, TransferMode mode, ScriptImportDecisionInfo scriptImportDecisionInfo)
        {
            scriptImportDecisionInfo.mode = mode;
            var episodeFile = scriptImportDecisionInfo.episodeFile;
            var series = scriptImportDecisionInfo.localEpisode.Series;

            var scriptImportDecision = _scriptImportDecider.TryImport(episodeFilePath, destinationFilePath, scriptImportDecisionInfo);

            switch (scriptImportDecision)
            {
                case ScriptImportDecision.DeferMove:
                    _diskTransferService.TransferFile(episodeFilePath, destinationFilePath, mode);
                    break;
                case ScriptImportDecision.RenameRequested:
                    _updateMediaInfo.Update(episodeFile, series, false);
                    episodeFile.Path = null;
                    _renameEpisodeFiles.MoveEpisodeFile(episodeFile, series, episodeFile.Episodes);
                    break;
                case ScriptImportDecision.MoveComplete:
                    break;
            }
        }

        public EpisodeFile MoveEpisodeFile(EpisodeFile episodeFile, LocalEpisode localEpisode, ScriptImportDecisionInfo scriptImportDecisionInfo)
        {
            var filePath = _buildFileNames.BuildFilePath(localEpisode.Episodes, localEpisode.Series, episodeFile, Path.GetExtension(localEpisode.Path));

            EnsureEpisodeFolder(episodeFile, localEpisode, filePath);

            _logger.Debug("Moving episode file: {0} to {1}", episodeFile.Path, filePath);

            return TransferFile(episodeFile, localEpisode.Series, localEpisode.Episodes, filePath, TransferMode.Move, scriptImportDecisionInfo);
        }

        public EpisodeFile CopyEpisodeFile(EpisodeFile episodeFile, LocalEpisode localEpisode, ScriptImportDecisionInfo scriptImportDecisionInfo)
        {
            var filePath = _buildFileNames.BuildFilePath(localEpisode.Episodes, localEpisode.Series, episodeFile, Path.GetExtension(localEpisode.Path));

            EnsureEpisodeFolder(episodeFile, localEpisode, filePath);

            if (_configService.CopyUsingHardlinks)
            {
                _logger.Debug("Hardlinking episode file: {0} to {1}", episodeFile.Path, filePath);
                return TransferFile(episodeFile, localEpisode.Series, localEpisode.Episodes, filePath, TransferMode.HardLinkOrCopy, scriptImportDecisionInfo);
            }

            _logger.Debug("Copying episode file: {0} to {1}", episodeFile.Path, filePath);
            return TransferFile(episodeFile, localEpisode.Series, localEpisode.Episodes, filePath, TransferMode.Copy, scriptImportDecisionInfo);
        }
    }
}
