mergeInto(LibraryManager.library, {
    // WEBAudio is Unity's existing context, not a second independent AudioContext.
    IQAudioIsRunning__deps: ['$WEBAudio'],
    IQAudioIsRunning: function () {
        return WEBAudio.audioContext && WEBAudio.audioContext.state === 'running' ? 1 : 0;
    },
    IQAudioResume__deps: ['$WEBAudio'],
    IQAudioResume: function () {
        var context = WEBAudio.audioContext;
        if (!context || context.state === 'closed' || context.state === 'running') return;
        try {
            var result = context.resume();
            if (result && result.catch) result.catch(function (error) {
                console.warn('StoryIQ audio is still suspended:', error);
            });
        } catch (error) { console.warn('StoryIQ audio could not resume:', error); }
    }
});
